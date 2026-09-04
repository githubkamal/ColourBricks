using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Ledger;
using ColourBricks.Domain.Ledger;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Ledger;

public sealed class LedgerPostingService(AppDbContext db, TimeProvider timeProvider, IAuditService audit)
    : ILedgerPostingService
{
    public async Task PostAsync(LedgerPosting posting, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(posting.SourceType) || posting.SourceId <= 0)
        {
            throw new ArgumentException("A ledger posting needs a source type and a positive source id.");
        }

        if (posting.Legs.Count == 0)
        {
            throw new ArgumentException("A ledger posting needs at least one leg.");
        }

        // Note: this ledger is a directional journal, not a balance-invariant one —
        // Debit/Credit are per-dimension direction flags and each of plan.md §5.3's
        // balances is a targeted SUM. Callers compose legs per those conventions.
        List<long> categoryIds = posting.Legs.Select(l => l.CategoryId).Distinct().ToList();
        int knownCategories = await db.ExpenseCategories
            .CountAsync(c => categoryIds.Contains(c.Id), cancellationToken);
        if (knownCategories != categoryIds.Count)
        {
            throw new ArgumentException("A ledger posting leg references an unknown expense category.");
        }

        foreach (LedgerLeg leg in posting.Legs)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                EntryDate = posting.EntryDate,
                ProjectId = leg.ProjectId,
                AccountId = leg.AccountId,
                PartyId = leg.PartyId,
                CategoryId = leg.CategoryId,
                Debit = Money.Round(leg.Debit),
                Credit = Money.Round(leg.Credit),
                SourceType = posting.SourceType,
                SourceId = posting.SourceId,
                IsReversal = false,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReverseAsync(
        string sourceType, long sourceId, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reverse a ledger posting.", nameof(reason));
        }

        List<LedgerEntry> entries = await db.LedgerEntries
            .Where(e => e.SourceType == sourceType && e.SourceId == sourceId)
            .ToListAsync(cancellationToken);

        List<LedgerEntry> originals = entries.Where(e => !e.IsReversal).ToList();
        if (originals.Count == 0)
        {
            throw new LedgerSourceNotFoundException(sourceType, sourceId);
        }

        if (entries.Any(e => e.IsReversal))
        {
            throw new LedgerAlreadyReversedException(sourceType, sourceId);
        }

        DateOnly today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        foreach (LedgerEntry original in originals)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                EntryDate = today,
                ProjectId = original.ProjectId,
                AccountId = original.AccountId,
                PartyId = original.PartyId,
                CategoryId = original.CategoryId,
                Debit = original.Credit,
                Credit = original.Debit,
                SourceType = sourceType,
                SourceId = sourceId,
                IsReversal = true,
            });
        }

        // This is the single choke point every reversal in the system passes through
        // (plan.md §5.6 — a reversal mirrors, never deletes), so logging the reason
        // here, once, covers every "delete" a user performs anywhere: vendor
        // purchases/payments, loans, custom work, direct/field-officer expenses,
        // receipts, common expenses. The bank-reconciliation unlink flow additionally
        // logs its own "unreconcile"/"exclude" audit row with the same reason at its
        // own level — this row is the underlying record's, not a duplicate.
        audit.RecordAction(sourceType, "reverse", sourceId.ToString(), reason.Trim());

        await db.SaveChangesAsync(cancellationToken);
    }
}
