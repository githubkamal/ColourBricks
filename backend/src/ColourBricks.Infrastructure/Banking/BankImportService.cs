using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Banking;
using ColourBricks.Domain.Banking;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Banking;

/// <summary>
/// P4-T01 — staged bank-statement import. Parsing lands rows in a Draft
/// <see cref="ImportBatch"/>; the accountant maps each row to project(s) and removes
/// the rest; <c>Commit</c> promotes the survivors to <c>BankTransaction (Pending)</c>
/// with project hints. No settlement or ledger entry is created here.
/// </summary>
public sealed partial class BankImportService(
    AppDbContext db,
    IAuditService audit,
    IBankStatementParser parser,
    IBankStatementProfileService profiles) : IBankImportService
{
    public async Task<BankImportBatchDto> CreateDraftFromFileAsync(
        long accountId, string fileName, Stream content, long profileId, CancellationToken cancellationToken)
    {
        BankStatementProfileDto? profile = await profiles.GetAsync(profileId, cancellationToken);
        if (profile is null)
        {
            throw Fail("profileId", "The statement profile does not exist.");
        }

        if (profile.AccountId != accountId)
        {
            throw Fail("profileId", "That profile belongs to a different account.");
        }

        BankStatementParseResult parsed = parser.Parse(content, fileName, profile);
        if (parsed.Rows.Count == 0)
        {
            throw Fail("file", "No data rows were found below the header row.");
        }

        return await CreateDraftAsync(
            new CreateBankImportRequest(accountId, fileName, parsed.Rows), cancellationToken);
    }

    public async Task<BankImportBatchDto> CreateDraftAsync(
        CreateBankImportRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Accounts.AnyAsync(a => a.Id == request.AccountId, cancellationToken))
        {
            throw Fail("accountId", "The account does not exist.");
        }

        if (request.Rows.Count == 0)
        {
            throw Fail("rows", "The statement has no rows.");
        }

        var batch = new ImportBatch
        {
            AccountId = request.AccountId,
            FileName = string.IsNullOrWhiteSpace(request.FileName) ? "statement" : request.FileName.Trim(),
            Status = ImportBatchStatus.Draft,
        };

        foreach (ParsedBankRowInput r in request.Rows.OrderBy(r => r.SourceLineNo))
        {
            bool parsed = string.IsNullOrWhiteSpace(r.ParseError) && r.ValueDate is not null;
            batch.Rows.Add(new StagedBankRow
            {
                SourceLineNo = r.SourceLineNo,
                ValueDate = r.ValueDate,
                Narration = Trim(r.Narration, 1000),
                NormalisedNarration = NormaliseNarration(r.Narration),
                Debit = Money.Round(r.Debit),
                Credit = Money.Round(r.Credit),
                Balance = r.Balance is { } b ? Money.Round(b) : null,
                BankReference = Trim(r.BankReference, 120) is { Length: > 0 } bref ? bref : null,
                ParseState = parsed ? StagedRowState.Parsed : StagedRowState.Error,
                ParseError = parsed
                    ? null
                    : Trim(r.ParseError ?? "The row could not be parsed (missing value date).", 500),
                RawLine = r.RawLine,
            });
        }

        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);

        await FlagDuplicatesAsync(batch, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return (await GetAsync(batch.Id, cancellationToken))!;
    }

    public async Task<BankImportBatchDto?> GetAsync(long batchId, CancellationToken cancellationToken)
    {
        ImportBatch? batch = await db.ImportBatches.AsNoTracking()
            .Include(b => b.Rows).ThenInclude(r => r.Allocations)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
        if (batch is null)
        {
            return null;
        }

        Dictionary<long, string> projectNames = await ProjectNamesForBatchAsync(batch, cancellationToken);
        int committed = batch.Status == ImportBatchStatus.Committed
            ? await db.BankTransactions.CountAsync(t => t.ImportBatchId == batchId, cancellationToken)
            : 0;

        List<StagedBankRowDto> rows = batch.Rows
            .OrderBy(r => r.SourceLineNo)
            .Select(r => ToRowDto(r, projectNames))
            .ToList();

        var counts = new BankImportOutcomeCountsDto(
            Total: batch.Rows.Count,
            Mapped: batch.Rows.Count(r => !r.IsRemoved && Ready(r)),
            Unmapped: batch.Rows.Count(r => !r.IsRemoved && r.ParseState == StagedRowState.Parsed
                && r.DuplicateOfBankTransactionId is null && !Ready(r)),
            Removed: batch.Rows.Count(r => r.IsRemoved),
            Duplicate: batch.Rows.Count(r => !r.IsRemoved && r.DuplicateOfBankTransactionId is not null),
            ParseError: batch.Rows.Count(r => !r.IsRemoved && r.ParseState == StagedRowState.Error),
            Committed: committed);

        return new BankImportBatchDto(
            batch.Id, batch.AccountId, batch.FileName, batch.Status.ToString(),
            batch.CreatedAtUtc, batch.CreatedByUserId, counts, rows);
    }

    public async Task<StagedBankRowDto> SetRowAllocationsAsync(
        long batchId, long rowId, SetRowAllocationsRequest request, CancellationToken cancellationToken)
    {
        (ImportBatch batch, StagedBankRow row) = await LoadDraftRowAsync(batchId, rowId, cancellationToken);

        if (row.ParseState == StagedRowState.Error)
        {
            throw Fail("row", "A row with a parse error cannot be mapped; remove it instead.");
        }

        decimal target = row.Debit > 0m ? row.Debit : row.Credit;
        bool isCredit = row.Credit > 0m;

        List<ProjectAllocationInput> allocations = request.Allocations
            .Where(a => a.ProjectId > 0)
            .ToList();

        if (allocations.Count == 0)
        {
            throw Fail("allocations", "Assign the row to at least one project.");
        }

        if (isCredit && allocations.Count > 1)
        {
            throw Fail("allocations", "A credit must be assigned to exactly one project (BRD §34).");
        }

        if (allocations.Any(a => a.Amount <= 0m))
        {
            throw Fail("allocations", "Every project amount must be greater than zero.");
        }

        var projectIds = allocations.Select(a => a.ProjectId).Distinct().ToList();
        if (projectIds.Count != allocations.Count)
        {
            throw Fail("allocations", "A project appears more than once.");
        }

        int known = await db.Projects.CountAsync(p => projectIds.Contains(p.Id), cancellationToken);
        if (known != projectIds.Count)
        {
            throw Fail("allocations", "One of the projects does not exist.");
        }

        if (Money.Round(allocations.Sum(a => a.Amount)) != Money.Round(target))
        {
            throw Fail("allocations",
                $"The project amounts total {allocations.Sum(a => a.Amount):0.00} but the row is {target:0.00}.");
        }

        db.StagedBankRowAllocations.RemoveRange(row.Allocations);
        row.Allocations = allocations
            .Select(a => new StagedBankRowAllocation
            {
                StagedBankRowId = row.Id,
                ProjectId = a.ProjectId,
                Amount = Money.Round(a.Amount),
            })
            .ToList();

        await db.SaveChangesAsync(cancellationToken);

        Dictionary<long, string> names = await ProjectNamesForBatchAsync(batch, cancellationToken);
        return ToRowDto(row, names);
    }

    public async Task<bool> RemoveRowAsync(
        long batchId, long rowId, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw Fail("reason", "A reason is required to delete a staged row.");
        }

        (_, StagedBankRow row) = await LoadDraftRowAsync(batchId, rowId, cancellationToken);
        row.IsRemoved = true;
        // StagedBankRow isn't [Auditable] (it's a pre-commit draft, not yet a real
        // record) so nothing logs this automatically — this is the only audit trail
        // a discarded row leaves (BRD §65).
        audit.RecordAction("bank_reconciliation", "delete", rowId.ToString(), reason.Trim());
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<BankImportCommitResultDto> CommitAsync(long batchId, CancellationToken cancellationToken)
    {
        ImportBatch? batch = await db.ImportBatches
            .Include(b => b.Rows).ThenInclude(r => r.Allocations)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
        if (batch is null)
        {
            throw Fail("id", "The import batch does not exist.");
        }

        if (batch.Status != ImportBatchStatus.Draft)
        {
            throw Fail("id", $"This batch is {batch.Status} and can no longer be committed.");
        }

        await FlagDuplicatesAsync(batch, cancellationToken);

        // Parse errors are an outcome, not a blocker — a row that failed to parse can't
        // be "fixed" here, so it just stays staged, counted, and unpromoted. A duplicate
        // is different (client request, 2026-09-04): it must be explicitly removed before
        // commit, exactly like an unmapped row, rather than being silently skipped — the
        // accountant must look at it and decide, not have it vanish unnoticed.
        List<StagedBankRow> live = batch.Rows.Where(r => !r.IsRemoved).ToList();
        List<StagedBankRow> parseErrors = live.Where(r => r.ParseState == StagedRowState.Error).ToList();
        List<StagedBankRow> parsed = live.Where(r => r.ParseState == StagedRowState.Parsed).ToList();
        List<StagedBankRow> duplicates = parsed.Where(r => r.DuplicateOfBankTransactionId is not null).ToList();
        List<StagedBankRow> promotable = parsed.Where(r => r.DuplicateOfBankTransactionId is null).ToList();

        List<string> blocked = parsed
            .Where(r => !Ready(r))
            .OrderBy(r => r.SourceLineNo)
            .Select(r => $"line {r.SourceLineNo}: {BlockReason(r)}")
            .ToList();
        if (blocked.Count > 0)
        {
            throw Fail("rows",
                "Every kept row must be mapped to project(s) and be free of duplicates before "
                + "commit — remove any duplicate or unmapped row first. "
                + string.Join("; ", blocked));
        }

        async Task<List<BankTransaction>> BuildAsync(List<StagedBankRow> rows)
        {
            // Occurrence index = existing committed rows of the same identity + the row's
            // ordinal among this batch's promotable rows sharing that identity.
            Dictionary<string, int> committed = await CommittedCountsBySignatureAsync(
                batch.AccountId, rows, cancellationToken);
            var ordinals = new Dictionary<string, int>();
            var built = new List<BankTransaction>();

            foreach (StagedBankRow r in rows.OrderBy(r => r.SourceLineNo))
            {
                string sig = Signature(batch.AccountId, r.ValueDate!.Value, r.Debit, r.Credit,
                    r.NormalisedNarration, r.BankReference);
                int ordinal = ordinals.GetValueOrDefault(sig, 0);
                ordinals[sig] = ordinal + 1;
                int occurrenceIndex = committed.GetValueOrDefault(sig, 0) + ordinal;

                built.Add(new BankTransaction
                {
                    ImportBatchId = batch.Id,
                    AccountId = batch.AccountId,
                    ValueDate = r.ValueDate.Value,
                    Narration = r.Narration,
                    NormalisedNarration = r.NormalisedNarration,
                    Debit = r.Debit,
                    Credit = r.Credit,
                    RunningBalance = r.Balance,
                    BankReference = r.BankReference,
                    OccurrenceIndex = occurrenceIndex,
                    RowHash = Hash(sig, occurrenceIndex),
                    Status = BankTransactionStatus.Pending,
                    ProjectHints = r.Allocations
                        .Select(a => new BankTransactionProjectHint { ProjectId = a.ProjectId, Amount = a.Amount })
                        .ToList(),
                });
            }

            return built;
        }

        List<BankTransaction> transactions = await BuildAsync(promotable);
        db.BankTransactions.AddRange(transactions);
        batch.Status = ImportBatchStatus.Committed;

        audit.RecordAction("bank_reconciliation", "import_commit", batch.Id.ToString(),
            JsonSerializer.Serialize(new
            {
                accountId = batch.AccountId,
                batch.FileName,
                committed = promotable.Count,
                removed = batch.Rows.Count(r => r.IsRemoved),
                duplicate = duplicates.Count,
            }));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another import committed an overlapping row while this batch was open —
            // the only unique constraint here is BankTransaction.RowHash. Drop the
            // now-duplicate rows and commit the rest rather than failing the batch.
            foreach (BankTransaction t in transactions)
            {
                db.Entry(t).State = EntityState.Detached;
            }

            await FlagDuplicatesAsync(batch, cancellationToken);
            List<StagedBankRow> stillPromotable = batch.Rows
                .Where(r => !r.IsRemoved && r.ParseState == StagedRowState.Parsed
                    && r.DuplicateOfBankTransactionId is null)
                .ToList();
            duplicates = batch.Rows
                .Where(r => !r.IsRemoved && r.ParseState == StagedRowState.Parsed
                    && r.DuplicateOfBankTransactionId is not null)
                .ToList();

            db.BankTransactions.AddRange(await BuildAsync(stillPromotable));
            batch.Status = ImportBatchStatus.Committed;
            await db.SaveChangesAsync(cancellationToken);
            promotable = stillPromotable;
        }

        return new BankImportCommitResultDto(
            batch.Id, promotable.Count, batch.Rows.Count(r => r.IsRemoved),
            duplicates.Count, parseErrors.Count);
    }

    public async Task<bool> DiscardAsync(long batchId, CancellationToken cancellationToken)
    {
        ImportBatch? batch = await db.ImportBatches
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
        if (batch is null)
        {
            return false;
        }

        if (batch.Status == ImportBatchStatus.Committed)
        {
            throw Fail("id", "A committed batch cannot be discarded.");
        }

        batch.Status = ImportBatchStatus.Discarded;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<BankTransactionDto?> GetTransactionAsync(long id, CancellationToken cancellationToken)
    {
        BankTransaction? t = await db.BankTransactions.AsNoTracking()
            .Include(x => x.ProjectHints)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (t is null)
        {
            return null;
        }

        List<long> projectIds = t.ProjectHints.Select(h => h.ProjectId).Distinct().ToList();
        Dictionary<long, string> names = await db.Projects.AsNoTracking()
            .Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return new BankTransactionDto(
            t.Id, t.ImportBatchId, t.AccountId, t.ValueDate, t.Narration, t.Debit, t.Credit,
            t.RunningBalance, t.BankReference, t.OccurrenceIndex, t.Status.ToString(), t.ExclusionReason,
            t.ProjectHints
                .Select(h => new ProjectAllocationDto(h.ProjectId, names.GetValueOrDefault(h.ProjectId, ""), h.Amount))
                .ToList());
    }

    public async Task<bool> ExcludeTransactionAsync(long id, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw Fail("reason", "A reason is required to exclude a transaction.");
        }

        BankTransaction? t = await db.BankTransactions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (t is null)
        {
            return false;
        }

        if (t.Status == BankTransactionStatus.Reconciled)
        {
            throw Fail("id", "Unreconcile the transaction before excluding it.");
        }

        t.Status = BankTransactionStatus.Excluded;
        t.ExclusionReason = reason.Trim();
        audit.RecordAction("bank_reconciliation", "exclude", t.Id.ToString(), reason.Trim());
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task FlagDuplicatesAsync(ImportBatch batch, CancellationToken cancellationToken)
    {
        List<StagedBankRow> parsed = batch.Rows
            .Where(r => r.ParseState == StagedRowState.Parsed && !r.IsRemoved)
            .OrderBy(r => r.SourceLineNo)
            .ToList();
        if (parsed.Count == 0)
        {
            return;
        }

        Dictionary<string, int> committed = await CommittedCountsBySignatureAsync(
            batch.AccountId, parsed, cancellationToken);

        var seen = new Dictionary<string, int>();
        foreach (StagedBankRow r in parsed)
        {
            string sig = Signature(batch.AccountId, r.ValueDate!.Value, r.Debit, r.Credit,
                r.NormalisedNarration, r.BankReference);
            int ordinal = seen.GetValueOrDefault(sig, 0);
            seen[sig] = ordinal + 1;

            r.DuplicateOfBankTransactionId = committed.GetValueOrDefault(sig, 0) > ordinal
                ? await db.BankTransactions
                    .Where(t => t.RowHash == Hash(sig, ordinal))
                    .Select(t => (long?)t.Id)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;
        }
    }

    private async Task<Dictionary<string, int>> CommittedCountsBySignatureAsync(
        long accountId, List<StagedBankRow> rows, CancellationToken cancellationToken)
    {
        List<DateOnly> dates = rows.Where(r => r.ValueDate is not null)
            .Select(r => r.ValueDate!.Value).Distinct().ToList();
        if (dates.Count == 0)
        {
            return [];
        }

        var existing = await db.BankTransactions.AsNoTracking()
            .Where(t => t.AccountId == accountId && dates.Contains(t.ValueDate))
            .Select(t => new { t.ValueDate, t.Debit, t.Credit, t.NormalisedNarration, t.BankReference })
            .ToListAsync(cancellationToken);

        return existing
            .GroupBy(t => Signature(accountId, t.ValueDate, t.Debit, t.Credit, t.NormalisedNarration, t.BankReference))
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<(ImportBatch Batch, StagedBankRow Row)> LoadDraftRowAsync(
        long batchId, long rowId, CancellationToken cancellationToken)
    {
        ImportBatch? batch = await db.ImportBatches
            .Include(b => b.Rows).ThenInclude(r => r.Allocations)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
        if (batch is null)
        {
            throw Fail("id", "The import batch does not exist.");
        }

        if (batch.Status != ImportBatchStatus.Draft)
        {
            throw Fail("id", $"This batch is {batch.Status} and can no longer be edited.");
        }

        StagedBankRow? row = batch.Rows.FirstOrDefault(r => r.Id == rowId);
        if (row is null)
        {
            throw Fail("rowId", "The row is not part of this batch.");
        }

        return (batch, row);
    }

    private async Task<Dictionary<long, string>> ProjectNamesForBatchAsync(
        ImportBatch batch, CancellationToken cancellationToken)
    {
        List<long> ids = batch.Rows
            .SelectMany(r => r.Allocations.Select(a => a.ProjectId))
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await db.Projects.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);
    }

    private static StagedBankRowDto ToRowDto(StagedBankRow r, Dictionary<long, string> projectNames)
    {
        decimal allocated = Money.Round(r.Allocations.Sum(a => a.Amount));
        bool ready = Ready(r);
        return new StagedBankRowDto(
            r.Id, r.SourceLineNo, r.ValueDate, r.Narration, r.Debit, r.Credit, r.Balance, r.BankReference,
            r.ParseState.ToString(), r.ParseError, r.DuplicateOfBankTransactionId, r.IsRemoved,
            r.Allocations
                .OrderBy(a => a.Id)
                .Select(a => new ProjectAllocationDto(
                    a.ProjectId, projectNames.GetValueOrDefault(a.ProjectId, ""), a.Amount))
                .ToList(),
            allocated,
            ready,
            ready || r.IsRemoved ? null : BlockReason(r));
    }

    private static bool Ready(StagedBankRow r)
    {
        if (r.IsRemoved || r.ParseState == StagedRowState.Error || r.DuplicateOfBankTransactionId is not null)
        {
            return false;
        }

        decimal target = r.Debit > 0m ? r.Debit : r.Credit;
        if (r.Allocations.Count == 0 || target <= 0m)
        {
            return false;
        }

        if (r.Credit > 0m && r.Allocations.Count != 1)
        {
            return false;
        }

        return Money.Round(r.Allocations.Sum(a => a.Amount)) == Money.Round(target);
    }

    private static string BlockReason(StagedBankRow r)
    {
        if (r.ParseState == StagedRowState.Error)
        {
            return r.ParseError ?? "parse error";
        }

        if (r.DuplicateOfBankTransactionId is not null)
        {
            return "already imported";
        }

        decimal target = r.Debit > 0m ? r.Debit : r.Credit;
        if (target <= 0m)
        {
            return "the row has no debit or credit amount";
        }

        if (r.Allocations.Count == 0)
        {
            return "not mapped to a project";
        }

        if (r.Credit > 0m && r.Allocations.Count != 1)
        {
            return "a credit must be a single project";
        }

        return $"project amounts total {r.Allocations.Sum(a => a.Amount):0.00}, not {target:0.00}";
    }

    private static string Signature(
        long accountId, DateOnly valueDate, decimal debit, decimal credit,
        string normalisedNarration, string? bankReference) =>
        string.Join('|',
            accountId,
            valueDate.ToString("yyyy-MM-dd"),
            debit.ToString("0.00"),
            credit.ToString("0.00"),
            normalisedNarration,
            bankReference ?? "");

    private static string Hash(string signature, int occurrenceIndex)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{signature}|{occurrenceIndex}"));
        return Convert.ToHexStringLower(bytes);
    }

    private static string NormaliseNarration(string? narration) =>
        Whitespace().Replace((narration ?? "").Trim().ToUpperInvariant(), " ");

    private static string Trim(string? value, int max)
    {
        string v = (value ?? "").Trim();
        return v.Length <= max ? v : v[..max];
    }

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
