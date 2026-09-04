using System.Text.Json;
using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Allocations;
using ColourBricks.Application.Banking;
using ColourBricks.Application.CommonExpenses;
using ColourBricks.Application.CustomWork;
using ColourBricks.Application.DirectExpenses;
using ColourBricks.Application.Ledger;
using ColourBricks.Application.Receipts;
using ColourBricks.Application.VendorPayments;
using ColourBricks.Domain.Banking;
using ColourBricks.Domain.Services;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Banking;

/// <summary>
/// P4-T05..T09 — the reconciliation queue plus credit/debit reconciliation,
/// internal transfers and unreconcile. A reconciled bank row is linked to exactly
/// one settlement; that settlement is either an existing manual one (no new money)
/// or one this service creates. Nothing here counts a rupee twice (BRD §37).
/// </summary>
public sealed class ReconciliationService(
    AppDbContext db,
    ILedgerPostingService ledger,
    IExpenseCategoryService categories,
    IAllocationEngine allocationEngine,
    IMatchSuggestionService matcher,
    IReceiptService receipts,
    IMultiProjectVendorPaymentService vendorPayments,
    IVendorPaymentService vendorPaymentAdmin,
    ICommonExpenseService commonExpenses,
    ICustomWorkPaymentService customWorkPayments,
    IDirectExpenseService directExpenses,
    IAuditService audit,
    TimeProvider clock) : IReconciliationService
{
    private const string SettlementReason = "Reconciled from bank statement";

    // ── queue (P4-T05) ───────────────────────────────────────────────────────

    public async Task<ReconciliationQueueDto> QueueAsync(ReconciliationQueueQuery q, CancellationToken ct)
    {
        IQueryable<BankTransaction> query = db.BankTransactions.AsNoTracking();

        if (q.AccountId is { } acc) query = query.Where(t => t.AccountId == acc);
        if (q.DateFrom is { } f) query = query.Where(t => t.ValueDate >= f);
        if (q.DateTo is { } t2) query = query.Where(t => t.ValueDate <= t2);
        if (q.AmountMin is { } lo) query = query.Where(t => t.Debit >= lo || t.Credit >= lo);
        if (q.AmountMax is { } hi) query = query.Where(t => t.Debit <= hi && t.Credit <= hi);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(t => t.Narration.Contains(q.Search));
        if (!string.IsNullOrWhiteSpace(q.Status)
            && Enum.TryParse(q.Status, ignoreCase: true, out BankTransactionStatus st))
            query = query.Where(t => t.Status == st);
        if (q.Matched is true) query = query.Where(t => t.Status == BankTransactionStatus.Reconciled);
        if (q.Matched is false) query = query.Where(t => t.Status == BankTransactionStatus.Pending);

        int total = await query.CountAsync(ct);
        int pageSize = Math.Clamp(q.PageSize, 1, 500);
        int page = Math.Max(1, q.Page);

        List<BankTransaction> rows = await query
            .OrderBy(t => t.ValueDate).ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        List<long> ids = rows.Select(r => r.Id).ToList();
        Dictionary<string, string> accountNames = await db.Accounts.AsNoTracking()
            .ToDictionaryAsync(a => a.Id.ToString(), a => a.Name, ct);

        var hints = (await db.BankTransactionProjectHints.AsNoTracking()
                .Where(h => ids.Contains(h.BankTransactionId))
                .Join(db.Projects.AsNoTracking(), h => h.ProjectId, p => p.Id,
                    (h, p) => new { h.BankTransactionId, h.ProjectId, p.Name, h.Amount })
                .ToListAsync(ct))
            .GroupBy(h => h.BankTransactionId)
            .ToDictionary(g => g.Key, g => g.Select(x =>
                new ProjectAllocationDto(x.ProjectId, x.Name, x.Amount)).ToList());

        // A transaction can now carry several active links — a split map across vendors
        // and/or Personal/Office/Savings (client request, 2026-09-04) — so group rather
        // than assume exactly one settlement per transaction.
        var links = await db.ReconciliationLinks.AsNoTracking()
            .Where(l => ids.Contains(l.BankTransactionId) && l.UnlinkedAtUtc == null)
            .ToListAsync(ct);
        Dictionary<long, List<long>> settlementIdsByTx = links
            .Where(l => l.SettlementId is not null
                && (l.Kind == ReconciliationLinkKind.Settlement || l.Kind == ReconciliationLinkKind.CustomWorkPayment))
            .GroupBy(l => l.BankTransactionId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.SettlementId!.Value).ToList());
        List<long> linkedSettlementIds = settlementIdsByTx.Values.SelectMany(v => v).Distinct().ToList();

        var linkedAlloc = (await db.Allocations.AsNoTracking()
                .Where(a => a.SettlementId != null && linkedSettlementIds.Contains(a.SettlementId.Value)
                    && a.ProjectId != null)
                .Join(db.Projects.AsNoTracking(), a => a.ProjectId!.Value, p => p.Id,
                    (a, p) => new { SettlementId = a.SettlementId!.Value, ProjectId = a.ProjectId!.Value, p.Name, a.Amount })
                .ToListAsync(ct))
            .GroupBy(a => a.SettlementId)
            .ToDictionary(g => g.Key, g => g.Select(x =>
                new ProjectAllocationDto(x.ProjectId, x.Name, x.Amount)).ToList());

        var items = rows.Select(r =>
        {
            List<ProjectAllocationDto> detail =
                settlementIdsByTx.TryGetValue(r.Id, out List<long>? sids)
                    ? sids.SelectMany(sid => linkedAlloc.GetValueOrDefault(sid, [])).ToList()
                    : hints.GetValueOrDefault(r.Id, []);
            decimal allocated = Money.Round(detail.Sum(d => d.Amount));
            decimal amount = r.Debit > 0m ? r.Debit : r.Credit;
            string projects = detail.Count switch { 0 => "", 1 => detail[0].ProjectName, var n => $"{n} Projects" };

            return new ReconciliationRowDto(
                r.Id, r.ValueDate, accountNames.GetValueOrDefault(r.AccountId.ToString(), ""),
                r.Narration, r.Debit > 0m ? "Debit" : "Credit", r.Credit, r.Debit,
                null, projects, allocated,
                r.Status == BankTransactionStatus.Reconciled ? 0m : Money.Round(amount - allocated),
                r.Status.ToString(), detail);
        }).ToList();

        return new ReconciliationQueueDto(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<ReconciliationProposalDto> ProposalAsync(long bankTransactionId, long? vendorId, CancellationToken ct)
    {
        BankTransaction tx = await Load(bankTransactionId, ct);
        decimal amount = tx.Debit > 0m ? tx.Debit : tx.Credit;

        List<ProjectAllocationDto> hints = (await db.BankTransactionProjectHints.AsNoTracking()
                .Where(h => h.BankTransactionId == bankTransactionId)
                .Join(db.Projects.AsNoTracking(), h => h.ProjectId, p => p.Id,
                    (h, p) => new ProjectAllocationDto(h.ProjectId, p.Name, h.Amount))
                .ToListAsync(ct));

        List<ProjectAllocationDto> fifo = [];
        if (tx.Debit > 0m && vendorId is { } vid)
        {
            AllocationProposalDto proposal = await allocationEngine.ProposeAsync(vid, amount, "Fifo", ct);
            fifo = proposal.Lines
                .Select(l => new ProjectAllocationDto(l.ProjectId, l.ProjectName, l.Allocated))
                .ToList();
        }

        return new ReconciliationProposalDto(
            bankTransactionId, tx.Debit > 0m ? "Debit" : "Credit", amount, hints, fifo);
    }

    // ── credit reconciliation (P4-T06) ───────────────────────────────────────

    public async Task<ReconcileResultDto> ReconcileCreditAsync(
        long bankTransactionId, ReconcileCreditRequest request, CancellationToken ct)
    {
        BankTransaction tx = await LoadPending(bankTransactionId, ct);
        if (tx.Credit <= 0m)
        {
            throw Fail("bankTransactionId", "This is not a credit transaction.");
        }

        if (!await db.Projects.AnyAsync(p => p.Id == request.ProjectId, ct))
        {
            throw Fail("projectId", "The project does not exist.");
        }

        long settlementId;
        bool created;

        if (request.ExistingReceiptId is { } receiptId)
        {
            Settlement existing = await db.Settlements
                .FirstOrDefaultAsync(s => s.Id == receiptId
                    && s.Direction == SettlementDirection.In
                    && s.Status == SettlementStatus.Active, ct)
                ?? throw Fail("existingReceiptId", "The receipt does not exist.");
            if (Money.Round(existing.Amount) != Money.Round(tx.Credit))
            {
                throw Fail("existingReceiptId", "The receipt amount does not match the bank credit.");
            }

            await GuardNotLinked(existing.Id, "receipt", ct);
            settlementId = existing.Id;
            created = false;
        }
        else
        {
            var income = Enum.TryParse(request.IncomeType, ignoreCase: true, out IncomeType it) ? it : IncomeType.ClientAdvance;
            ReceiptDto dto = await receipts.RecordAsync(new RecordReceiptRequest(
                request.ProjectId, income, tx.ValueDate, tx.Credit,
                await BankTransferModeId(ct), tx.AccountId, ReconRef(tx), SettlementReason), ct);
            settlementId = dto.Id;
            created = true;
        }

        await LinkAsync(tx, settlementId, created, ct);
        await matcher.RememberAliasesAsync(request.ClientId, tx.Narration, ct);
        RecordReconcileAudit(tx, settlementId, "credit");
        await db.SaveChangesAsync(ct);

        return new ReconcileResultDto(tx.Id, settlementId, created, tx.Status.ToString());
    }

    // ── debit reconciliation (P4-T07) ────────────────────────────────────────

    public async Task<ReconcileResultDto> ReconcileDebitAsync(
        long bankTransactionId, ReconcileDebitRequest request, CancellationToken ct)
    {
        BankTransaction tx = await LoadPending(bankTransactionId, ct);
        if (tx.Debit <= 0m)
        {
            throw Fail("bankTransactionId", "This is not a debit transaction.");
        }

        long settlementId;
        bool created;

        if (request.ExistingPaymentId is { } paymentId)
        {
            Settlement existing = await db.Settlements
                .FirstOrDefaultAsync(s => s.Id == paymentId
                    && s.Direction == SettlementDirection.Out
                    && s.Status == SettlementStatus.Active, ct)
                ?? throw Fail("existingPaymentId", "The payment does not exist.");
            if (Money.Round(existing.Amount) != Money.Round(tx.Debit))
            {
                throw Fail("existingPaymentId", "The payment amount does not match the bank debit.");
            }

            await GuardNotLinked(existing.Id, "payment", ct);
            settlementId = existing.Id;
            created = false;
        }
        else
        {
            if (request.VendorId is not { } vendorId)
            {
                throw Fail("vendorId", "Pick a vendor or link an existing payment.");
            }

            if (request.Allocations is null || request.Allocations.Count == 0)
            {
                throw Fail("allocations", "Allocate the debit to at least one project.");
            }

            decimal sum = Money.Round(request.Allocations.Sum(a => a.Amount));
            if (sum != Money.Round(tx.Debit))
            {
                throw Fail("allocations",
                    $"Allocations total {sum:0.00}; they must equal the bank debit of {tx.Debit:0.00} (rule 48).");
            }

            MultiProjectPaymentDto paid = await vendorPayments.PayAsync(new RecordMultiProjectPaymentRequest(
                vendorId, tx.ValueDate, tx.Debit, await BankTransferModeId(ct), tx.AccountId, ReconRef(tx),
                request.Allocations.Select(a => new AllocationInputDto(a.ProjectId, null, a.Amount)).ToList(),
                SettlementReason), ct);
            settlementId = paid.SettlementId;
            created = true;

            await matcher.RememberAliasesAsync(vendorId, tx.Narration, ct);
        }

        await LinkAsync(tx, settlementId, created, ct);
        RecordReconcileAudit(tx, settlementId, "debit");
        await db.SaveChangesAsync(ct);

        return new ReconcileResultDto(tx.Id, settlementId, created, tx.Status.ToString());
    }

    // ── debit map/split (client request, 2026-09-04) ─────────────────────────

    public async Task<MapDebitResultDto> MapDebitAsync(
        long bankTransactionId, MapDebitRequest request, CancellationToken ct)
    {
        BankTransaction tx = await LoadPending(bankTransactionId, ct);
        if (tx.Debit <= 0m)
        {
            throw Fail("bankTransactionId", "This is not a debit transaction.");
        }

        IReadOnlyList<ReconciliationAllocationInput> lines = request.Allocations;
        if (lines is null || lines.Count == 0)
        {
            throw Fail("allocations", "Split the debit into at least one line.");
        }

        for (int i = 0; i < lines.Count; i++)
        {
            ReconciliationAllocationInput line = lines[i];
            if (line.Amount <= 0m)
            {
                throw Fail($"allocations[{i}].amount", "Each split amount must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(line.Description))
            {
                throw Fail($"allocations[{i}].description", "Each split needs a description.");
            }

            if (IsParty(line.Target))
            {
                if (line.VendorId is null)
                {
                    throw Fail($"allocations[{i}].vendorId", $"Pick a {(line.Target == ReconciliationAllocationTarget.FieldOfficer ? "field officer" : "vendor")} for this split.");
                }
            }
            else if (line.Target == ReconciliationAllocationTarget.CustomWork)
            {
                if (line.VendorId is null)
                {
                    throw Fail($"allocations[{i}].vendorId", "Pick the party this custom work is owed to.");
                }

                if (line.ProjectId is null)
                {
                    throw Fail($"allocations[{i}].projectId", "Pick the project this custom work belongs to.");
                }
            }
            else if (line.Target == ReconciliationAllocationTarget.BankCharges)
            {
                if (line.ProjectId is null)
                {
                    throw Fail($"allocations[{i}].projectId", "Pick the project this charge belongs to.");
                }

                if (line.VendorId is not null)
                {
                    throw Fail($"allocations[{i}].vendorId", "A bank charge has no party.");
                }
            }
            else if (line.ProjectId is not null || line.VendorId is not null)
            {
                throw Fail($"allocations[{i}].target",
                    "A Personal/Office/Savings/Custom split cannot carry a project or vendor.");
            }
        }

        decimal sum = Money.Round(lines.Sum(a => a.Amount));
        if (sum != Money.Round(tx.Debit))
        {
            throw Fail("allocations",
                $"The split totals {sum:0.00}; it must equal the bank debit of {tx.Debit:0.00} (rule 48).");
        }

        var settlementIds = new List<long>();
        var commonExpenseIds = new List<long>();
        var directExpenseIds = new List<long>();
        long bankMode = await BankTransferModeId(ct);

        foreach (var vendorGroup in lines
            .Where(a => IsParty(a.Target))
            .GroupBy(a => a.VendorId!.Value))
        {
            List<AllocationInputDto> projectLines = vendorGroup
                .Where(a => a.ProjectId is not null)
                .GroupBy(a => a.ProjectId!.Value)
                .Select(g => new AllocationInputDto(g.Key, null, Money.Round(g.Sum(x => x.Amount))))
                .ToList();
            decimal advance = Money.Round(vendorGroup.Where(a => a.ProjectId is null).Sum(a => a.Amount));
            decimal vendorTotal = Money.Round(projectLines.Sum(l => l.Amount) + advance);

            MultiProjectPaymentDto paid = await vendorPayments.PayAsync(new RecordMultiProjectPaymentRequest(
                vendorGroup.Key, tx.ValueDate, vendorTotal, bankMode, tx.AccountId, ReconRef(tx),
                projectLines.Count > 0 ? projectLines : null, SettlementReason, advance), ct);

            AddSettlementLink(tx, paid.SettlementId, created: true);
            settlementIds.Add(paid.SettlementId);
            await matcher.RememberAliasesAsync(vendorGroup.Key, tx.Narration, ct);
        }

        foreach (ReconciliationAllocationInput line in lines
            .Where(a => a.Target == ReconciliationAllocationTarget.CustomWork))
        {
            CustomWorkPaymentDto paid = await customWorkPayments.PayAsync(new RecordCustomWorkPaymentRequest(
                line.ProjectId!.Value, line.VendorId!.Value, tx.ValueDate, line.Amount,
                bankMode, tx.AccountId, ReconRef(tx)), ct);

            AddSettlementLink(tx, paid.Id, created: true, ReconciliationLinkKind.CustomWorkPayment);
            settlementIds.Add(paid.Id);
        }

        foreach (ReconciliationAllocationInput line in lines
            .Where(a => a.Target == ReconciliationAllocationTarget.BankCharges))
        {
            long otherExpenseCategory = await categories.RequireIdAsync("other_expenses", ct);
            string description = line.Description.Trim();
            DirectExpenseDto expense = await directExpenses.RecordAsync(new RecordDirectExpenseRequest(
                line.ProjectId!.Value, otherExpenseCategory, tx.ValueDate, line.Amount,
                PaidImmediately: true, PartyId: null, PaymentModeId: bankMode, AccountId: tx.AccountId,
                ReferenceNo: ReconRef(tx), Description: description), ct);

            AddObligationLink(tx, expense.Id);
            directExpenseIds.Add(expense.Id);
        }

        foreach (ReconciliationAllocationInput line in lines
            .Where(a => !IsParty(a.Target)
                && a.Target != ReconciliationAllocationTarget.CustomWork
                && a.Target != ReconciliationAllocationTarget.BankCharges))
        {
            string description = line.Description.Trim();
            CommonExpenseDto expense = await commonExpenses.RecordAsync(new RecordCommonExpenseRequest(
                line.Target.ToString(), description, tx.ValueDate, line.Amount,
                bankMode, tx.AccountId, ReconRef(tx), description), ct);

            AddCommonExpenseLink(tx, expense.Id);
            commonExpenseIds.Add(expense.Id);
        }

        MarkReconciled(tx);
        audit.RecordAction("bank_reconciliation", "map_debit", tx.Id.ToString(),
            JsonSerializer.Serialize(new
            {
                bankReference = tx.BankReference,
                amount = tx.Debit,
                settlementIds,
                commonExpenseIds,
                directExpenseIds,
                splits = lines,
            }));
        await db.SaveChangesAsync(ct);

        return new MapDebitResultDto(tx.Id, tx.Status.ToString(), settlementIds, commonExpenseIds, directExpenseIds);
    }

    // ── hold (client request, 2026-09-04) ─────────────────────────────────────

    public async Task HoldAsync(long bankTransactionId, CancellationToken ct)
    {
        BankTransaction tx = await Load(bankTransactionId, ct);
        if (tx.Status != BankTransactionStatus.Pending)
        {
            throw Fail("bankTransactionId", $"Only a pending transaction can be held (it is {tx.Status}).");
        }

        tx.Status = BankTransactionStatus.InReview;
        audit.RecordAction("bank_reconciliation", "hold", tx.Id.ToString(), "");
        await db.SaveChangesAsync(ct);
    }

    public async Task UnholdAsync(long bankTransactionId, CancellationToken ct)
    {
        BankTransaction tx = await Load(bankTransactionId, ct);
        if (tx.Status != BankTransactionStatus.InReview)
        {
            throw Fail("bankTransactionId", $"This transaction is not on hold (it is {tx.Status}).");
        }

        tx.Status = BankTransactionStatus.Pending;
        audit.RecordAction("bank_reconciliation", "unhold", tx.Id.ToString(), "");
        await db.SaveChangesAsync(ct);
    }

    // ── unreconcile (P4-T09) ─────────────────────────────────────────────────

    public async Task UnreconcileAsync(long bankTransactionId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw Fail("reason", "A reason is required to unreconcile.");
        }

        BankTransaction tx = await Load(bankTransactionId, ct);
        List<ReconciliationLink> links = await db.ReconciliationLinks
            .Where(l => l.BankTransactionId == bankTransactionId && l.UnlinkedAtUtc == null)
            .ToListAsync(ct);
        if (links.Count == 0)
        {
            throw Fail("bankTransactionId", "This transaction is not reconciled.");
        }

        // A mapped debit can carry several links (a split across vendors and/or
        // Personal/Office/Savings — client request, 2026-09-04); reverse every one of
        // them, dispatching by Kind rather than assuming they're all the same shape.
        foreach (ReconciliationLink link in links)
        {
            if (link.SettlementCreatedByReconciliation)
            {
                if (link.Kind == ReconciliationLinkKind.CommonExpense)
                {
                    await commonExpenses.ReverseAsync(link.CommonExpenseId!.Value, reason, ct);
                }
                else if (link.Kind == ReconciliationLinkKind.CustomWorkPayment)
                {
                    await customWorkPayments.ReverseAsync(link.SettlementId!.Value, reason, ct);
                }
                else if (link.Kind == ReconciliationLinkKind.DirectExpense)
                {
                    await directExpenses.ReverseAsync(link.ObligationId!.Value, reason, ct);
                }
                else if (tx.Debit > 0m)
                {
                    await vendorPaymentAdmin.ReverseAsync(link.SettlementId!.Value, reason, ct);
                }
                else
                {
                    await receipts.ReverseAsync(link.SettlementId!.Value, reason, ct);
                }
            }

            link.UnlinkedAtUtc = clock.GetUtcNow();
            link.UnlinkReason = reason.Trim();
        }

        tx.Status = BankTransactionStatus.Pending;

        audit.RecordAction("bank_reconciliation", "unreconcile", tx.Id.ToString(),
            JsonSerializer.Serialize(new
            {
                bankReference = tx.BankReference,
                links = links.Select(l => new
                {
                    l.Kind,
                    l.SettlementId,
                    l.CommonExpenseId,
                    settlementReversed = l.SettlementCreatedByReconciliation,
                }),
                reason = reason.Trim(),
            }));
        await db.SaveChangesAsync(ct);
    }

    public async Task BulkExcludeAsync(IReadOnlyList<long> ids, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw Fail("reason", "A reason is required to exclude transactions.");
        }

        List<BankTransaction> rows = await db.BankTransactions
            .Where(t => ids.Contains(t.Id) && t.Status != BankTransactionStatus.Reconciled)
            .ToListAsync(ct);
        foreach (BankTransaction t in rows)
        {
            t.Status = BankTransactionStatus.Excluded;
            t.ExclusionReason = reason.Trim();
            audit.RecordAction("bank_reconciliation", "exclude", t.Id.ToString(), reason.Trim());
        }

        await db.SaveChangesAsync(ct);
    }

    // ── internal transfers (P4-T08) ──────────────────────────────────────────

    public async Task<IReadOnlyList<InternalTransferSuggestionDto>> TransferSuggestionsAsync(
        long? accountId, CancellationToken ct)
    {
        List<BankTransaction> pending = await db.BankTransactions.AsNoTracking()
            .Where(t => t.Status == BankTransactionStatus.Pending
                && (accountId == null || t.AccountId == accountId))
            .ToListAsync(ct);

        var debits = pending.Where(t => t.Debit > 0m).ToList();
        var credits = pending.Where(t => t.Credit > 0m).ToList();
        var used = new HashSet<long>();
        var result = new List<InternalTransferSuggestionDto>();

        foreach (BankTransaction d in debits)
        {
            BankTransaction? c = credits.FirstOrDefault(x =>
                !used.Contains(x.Id)
                && x.AccountId != d.AccountId
                && Math.Abs(x.Credit - d.Debit) <= Money.Tolerance
                && Math.Abs(x.ValueDate.DayNumber - d.ValueDate.DayNumber) <= 5);
            if (c is null)
            {
                continue;
            }

            used.Add(c.Id);
            result.Add(new InternalTransferSuggestionDto(
                d.Id, c.Id, d.AccountId, c.AccountId, d.Debit, d.ValueDate));
        }

        return result;
    }

    public async Task<InternalTransferDto> PairTransferAsync(PairInternalTransferRequest request, CancellationToken ct)
    {
        BankTransaction from = await LoadPending(request.FromTransactionId, ct);
        BankTransaction to = await LoadPending(request.ToTransactionId, ct);

        if (from.Id == to.Id || from.Debit <= 0m || to.Credit <= 0m)
        {
            throw Fail("fromTransactionId", "Pair one debit with one credit.");
        }

        if (from.AccountId == to.AccountId)
        {
            throw Fail("toTransactionId", "An internal transfer must be between two different accounts.");
        }

        if (Math.Abs(from.Debit - to.Credit) > Money.Tolerance)
        {
            throw Fail("toTransactionId", "The two amounts must match.");
        }

        if (Math.Abs(from.ValueDate.DayNumber - to.ValueDate.DayNumber) > 5)
        {
            throw Fail("toTransactionId", "The two dates are too far apart for a transfer.");
        }

        var transfer = new InternalTransfer
        {
            FromTransactionId = from.Id,
            ToTransactionId = to.Id,
            FromAccountId = from.AccountId,
            ToAccountId = to.AccountId,
            Amount = from.Debit,
            Date = from.ValueDate,
        };
        db.InternalTransfers.Add(transfer);
        await db.SaveChangesAsync(ct);

        long category = await categories.RequireIdAsync("internal_transfer", ct);
        await ledger.PostAsync(new LedgerPosting("InternalTransfer", transfer.Id, from.ValueDate,
        [
            new LedgerLeg(category, Debit: transfer.Amount, Credit: 0m, AccountId: from.AccountId),
            new LedgerLeg(category, Debit: 0m, Credit: transfer.Amount, AccountId: to.AccountId),
        ]), ct);

        from.Status = BankTransactionStatus.InternalTransfer;
        to.Status = BankTransactionStatus.InternalTransfer;
        audit.RecordAction("bank_reconciliation", "internal_transfer", transfer.Id.ToString(),
            JsonSerializer.Serialize(new { from.AccountId, toAccountId = to.AccountId, transfer.Amount }));
        await db.SaveChangesAsync(ct);

        return new InternalTransferDto(
            transfer.Id, from.Id, to.Id, from.AccountId, to.AccountId, transfer.Amount, transfer.Date);
    }

    public async Task UnpairTransferAsync(long internalTransferId, CancellationToken ct)
    {
        InternalTransfer transfer = await db.InternalTransfers
            .FirstOrDefaultAsync(x => x.Id == internalTransferId && x.UnpairedAtUtc == null, ct)
            ?? throw Fail("id", "The internal transfer does not exist or is already unpaired.");

        await ledger.ReverseAsync("InternalTransfer", transfer.Id, "Unpaired", ct);

        foreach (BankTransaction t in await db.BankTransactions
            .Where(t => t.Id == transfer.FromTransactionId || t.Id == transfer.ToTransactionId)
            .ToListAsync(ct))
        {
            t.Status = BankTransactionStatus.Pending;
        }

        transfer.UnpairedAtUtc = clock.GetUtcNow();
        audit.RecordAction("bank_reconciliation", "internal_transfer_unpair", transfer.Id.ToString(), "");
        await db.SaveChangesAsync(ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    // Vendor and FieldOfficer are the same code path (a payment/advance to a party) —
    // only Personal/Office/Savings/Custom go through common expenses instead.
    private static bool IsParty(ReconciliationAllocationTarget target) =>
        target is ReconciliationAllocationTarget.Vendor or ReconciliationAllocationTarget.FieldOfficer;

    private async Task<BankTransaction> Load(long id, CancellationToken ct) =>
        await db.BankTransactions.FirstOrDefaultAsync(t => t.Id == id, ct)
        ?? throw Fail("bankTransactionId", "The bank transaction does not exist.");

    private async Task<BankTransaction> LoadPending(long id, CancellationToken ct)
    {
        BankTransaction tx = await Load(id, ct);
        if (tx.Status is not (BankTransactionStatus.Pending or BankTransactionStatus.InReview))
        {
            throw Fail("bankTransactionId", $"This transaction is {tx.Status} and cannot be reconciled.");
        }

        return tx;
    }

    private async Task GuardNotLinked(long settlementId, string kind, CancellationToken ct)
    {
        if (await db.ReconciliationLinks.AnyAsync(l => l.SettlementId == settlementId && l.UnlinkedAtUtc == null, ct))
        {
            throw Fail("existing" + char.ToUpperInvariant(kind[0]) + kind[1..] + "Id",
                $"That {kind} is already reconciled to another bank transaction.");
        }
    }

    private async Task LinkAsync(BankTransaction tx, long settlementId, bool created, CancellationToken ct)
    {
        AddSettlementLink(tx, settlementId, created);
        MarkReconciled(tx);
        await Task.CompletedTask;
    }

    private void AddSettlementLink(
        BankTransaction tx, long settlementId, bool created,
        ReconciliationLinkKind kind = ReconciliationLinkKind.Settlement) =>
        db.ReconciliationLinks.Add(new ReconciliationLink
        {
            BankTransactionId = tx.Id,
            Kind = kind,
            SettlementId = settlementId,
            MatchConfidence = 100,
            MatchMethod = MatchMethod.Manual,
            SettlementCreatedByReconciliation = created,
        });

    private void AddCommonExpenseLink(BankTransaction tx, long commonExpenseId) =>
        db.ReconciliationLinks.Add(new ReconciliationLink
        {
            BankTransactionId = tx.Id,
            Kind = ReconciliationLinkKind.CommonExpense,
            CommonExpenseId = commonExpenseId,
            MatchConfidence = 100,
            MatchMethod = MatchMethod.Manual,
            SettlementCreatedByReconciliation = true,
        });

    private void AddObligationLink(BankTransaction tx, long obligationId) =>
        db.ReconciliationLinks.Add(new ReconciliationLink
        {
            BankTransactionId = tx.Id,
            Kind = ReconciliationLinkKind.DirectExpense,
            ObligationId = obligationId,
            MatchConfidence = 100,
            MatchMethod = MatchMethod.Manual,
            SettlementCreatedByReconciliation = true,
        });

    private void MarkReconciled(BankTransaction tx)
    {
        tx.Status = BankTransactionStatus.Reconciled;
        tx.ReconciledAtUtc = clock.GetUtcNow();
    }

    private void RecordReconcileAudit(BankTransaction tx, long settlementId, string type) =>
        audit.RecordAction("bank_reconciliation", "reconcile", tx.Id.ToString(),
            JsonSerializer.Serialize(new
            {
                type,
                bankReference = tx.BankReference,
                valueDate = tx.ValueDate.ToString("yyyy-MM-dd"),
                amount = tx.Debit > 0m ? tx.Debit : tx.Credit,
                settlementId,
                accountId = tx.AccountId,
            }));

    private async Task<long> BankTransferModeId(CancellationToken ct) =>
        await db.PaymentModes.Where(m => m.Name == "Bank Transfer").Select(m => m.Id).FirstAsync(ct);

    // "Bank Transfer" mode requires a reference; a statement debit may not carry a UTR,
    // so fall back to a traceable synthetic one.
    private static string ReconRef(BankTransaction tx) =>
        tx.BankReference is { Length: > 0 } r ? r : $"BankTxn-{tx.Id}";

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
