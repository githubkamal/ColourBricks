using System.Text.RegularExpressions;
using ColourBricks.Application.Banking;
using ColourBricks.Domain.Banking;
using ColourBricks.Domain.Services;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Banking;

/// <summary>
/// P4-T04 — scores existing <c>Settlement</c> records against a bank transaction
/// (BRD §32). Amount must match; reference/UTR, date proximity and narration vs
/// party name / learned alias add confidence. It never reconciles anything —
/// suggestions only; the accountant confirms.
/// </summary>
public sealed partial class MatchSuggestionService(AppDbContext db) : IMatchSuggestionService
{
    private const int AutoSelectThreshold = 90;

    private static readonly HashSet<string> NarrationStopWords = new(StringComparer.Ordinal)
    {
        "NEFT", "RTGS", "IMPS", "UPI", "NACH", "ACH", "CHQ", "CHEQUE", "DR", "CR",
        "BIL", "POS", "ATM", "TO", "FROM", "PAYMENT", "PAYMT", "TRANSFER", "TRF",
        "REF", "ONLINE", "BANK", "INB", "MB", "AXIS", "HDFC", "ICICI", "SBI",
    };

    public async Task<MatchSuggestionsDto> SuggestAsync(long bankTransactionId, CancellationToken cancellationToken)
    {
        BankTransaction tx = await db.BankTransactions.AsNoTracking()
            .FirstAsync(t => t.Id == bankTransactionId, cancellationToken);

        decimal amount = tx.Debit > 0m ? tx.Debit : tx.Credit;
        SettlementDirection wanted = tx.Debit > 0m ? SettlementDirection.Out : SettlementDirection.In;

        List<long> reconciled = await db.ReconciliationLinks.AsNoTracking()
            .Where(l => l.UnlinkedAtUtc == null && l.SettlementId != null)
            .Select(l => l.SettlementId!.Value)
            .ToListAsync(cancellationToken);

        List<Settlement> candidates = await db.Settlements.AsNoTracking()
            .Where(s => s.Direction == wanted
                && s.Status == SettlementStatus.Active
                && s.Amount >= amount - Money.Tolerance && s.Amount <= amount + Money.Tolerance
                && !reconciled.Contains(s.Id))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return new MatchSuggestionsDto(bankTransactionId, AutoSelectThreshold, null, []);
        }

        List<long> partyIds = candidates.Where(c => c.PartyId != null).Select(c => c.PartyId!.Value).Distinct().ToList();
        Dictionary<long, string> partyNames = await db.Parties.AsNoTracking()
            .Where(p => partyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);
        Dictionary<long, string> normNames = await db.Parties.AsNoTracking()
            .Where(p => partyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => Normalise(p.Name), cancellationToken);
        var aliasesByParty = (await db.PartyAliases.AsNoTracking()
                .Where(a => partyIds.Contains(a.PartyId))
                .Select(a => new { a.PartyId, a.NormalisedAlias })
                .ToListAsync(cancellationToken))
            .GroupBy(a => a.PartyId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.NormalisedAlias).ToList());

        string narration = Normalise(tx.Narration);
        string? txRef = string.IsNullOrWhiteSpace(tx.BankReference) ? null : Normalise(tx.BankReference);

        var scored = candidates
            .Select(s =>
            {
                var reasons = new List<string> { "exact amount" };
                int score = 60;

                if (txRef is not null && !string.IsNullOrWhiteSpace(s.ReferenceNo)
                    && Normalise(s.ReferenceNo).Contains(txRef, StringComparison.Ordinal))
                {
                    score += 25;
                    reasons.Add("reference / UTR match");
                }

                int days = Math.Abs(tx.ValueDate.DayNumber - s.Date.DayNumber);
                if (days == 0) { score += 10; reasons.Add("same date"); }
                else if (days <= 3) { score += 7; reasons.Add("date within 3 days"); }
                else if (days <= 7) { score += 4; reasons.Add("date within a week"); }

                if (s.PartyId is { } pid)
                {
                    string nn = normNames.GetValueOrDefault(pid, "");
                    bool nameHit = nn.Length >= 4 && narration.Contains(nn, StringComparison.Ordinal);
                    bool aliasHit = aliasesByParty.GetValueOrDefault(pid, []).Any(
                        a => a.Length >= 4 && narration.Contains(a, StringComparison.Ordinal));
                    if (nameHit || aliasHit)
                    {
                        score += 15;
                        reasons.Add(aliasHit && !nameHit
                            ? $"narration matches a learned alias of {partyNames.GetValueOrDefault(pid, "")}"
                            : $"narration matches {partyNames.GetValueOrDefault(pid, "")}");
                    }
                }

                return new MatchSuggestionDto(
                    s.Id, s.PartyId, s.PartyId is { } p ? partyNames.GetValueOrDefault(p, "") : "",
                    s.Date, s.Amount, s.ReferenceNo, s.Description ?? "", Math.Min(score, 100), reasons);
            })
            .OrderByDescending(s => s.Score)
            .ThenBy(s => Math.Abs(tx.ValueDate.DayNumber - s.Date.DayNumber))
            .ToList();

        long? autoSelect = scored[0].Score >= AutoSelectThreshold
            && (scored.Count == 1 || scored[1].Score < scored[0].Score)
            ? scored[0].SettlementId
            : null;

        return new MatchSuggestionsDto(bankTransactionId, AutoSelectThreshold, autoSelect, scored);
    }

    public async Task RememberAliasesAsync(long partyId, string narration, CancellationToken cancellationToken)
    {
        List<string> existing = await db.PartyAliases.AsNoTracking()
            .Where(a => a.PartyId == partyId)
            .Select(a => a.NormalisedAlias)
            .ToListAsync(cancellationToken);
        var seen = existing.ToHashSet(StringComparer.Ordinal);

        foreach (string raw in TokenRegex().Split(narration.ToUpperInvariant()))
        {
            string token = new(raw.Where(char.IsLetterOrDigit).ToArray());
            if (token.Length < 4 || token.All(char.IsDigit) || NarrationStopWords.Contains(token))
            {
                continue;
            }

            if (seen.Add(token))
            {
                db.PartyAliases.Add(new PartyAlias { PartyId = partyId, Alias = raw.Trim(), NormalisedAlias = token });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    internal static string Normalise(string s) =>
        new(s.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    [GeneratedRegex(@"[^A-Za-z0-9]+")]
    private static partial Regex TokenRegex();
}
