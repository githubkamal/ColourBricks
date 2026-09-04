using ColourBricks.Application.Banking;
using ColourBricks.Application.Integrity;
using ColourBricks.Application.Ledger;
using ColourBricks.Application.Reporting.Framework;
using ColourBricks.Domain.Banking;
using ColourBricks.Domain.Services;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Reporting.Framework;

/// <summary>P8-T05 — the BRD §55 cash/bank reports and the BRD §38 reconciliation reports.</summary>
public sealed class CashBankReportRow : IReportRow
{
    public long? Id { get; init; }
    public DateOnly? Date { get; init; }
    public string? Account { get; init; }
    public string? Label { get; init; }
    public string? Narration { get; init; }
    public string? Status { get; init; }
    public string? Reason { get; init; }
    public string? User { get; init; }
    public decimal Opening { get; init; }
    public decimal Credits { get; init; }
    public decimal Debits { get; init; }
    public decimal Closing { get; init; }
    public decimal Received { get; init; }
    public decimal Paid { get; init; }
    public decimal Amount { get; init; }
    public decimal Allocated { get; init; }
    public decimal Difference { get; init; }
    public int Count { get; init; }
    public bool Passed { get; init; }

    public DateOnly? RowDate { get; init; }
    public long? RowProjectId => null;
    public long? RowPartyId => null;
    public long? RowDepartmentId => null;
    public long? RowItemId => null;
    public long? RowCategoryId => null;
    public long? RowPaymentModeId { get; init; }
    public long? RowAccountId { get; init; }
    public string? RowPaymentStatus => null;
    public string? RowTransactionType { get; init; }
    public string? RowReconciliationStatus { get; init; }
    public string? RowSearchText { get; init; }
}

internal static class CashBankSort
{
    public static readonly IReadOnlyDictionary<string, Func<IQueryable<CashBankReportRow>, bool, IOrderedQueryable<CashBankReportRow>>> ByDate =
        new Dictionary<string, Func<IQueryable<CashBankReportRow>, bool, IOrderedQueryable<CashBankReportRow>>>
        {
            ["date"] = (q, d) => d ? q.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
                                   : q.OrderBy(x => x.Date).ThenBy(x => x.Id),
        };

    public static readonly IReadOnlyDictionary<string, Func<IQueryable<CashBankReportRow>, bool, IOrderedQueryable<CashBankReportRow>>> ByLabel =
        new Dictionary<string, Func<IQueryable<CashBankReportRow>, bool, IOrderedQueryable<CashBankReportRow>>>
        {
            ["label"] = (q, d) => d ? q.OrderByDescending(x => x.Label) : q.OrderBy(x => x.Label),
            ["account"] = (q, d) => d ? q.OrderByDescending(x => x.Account) : q.OrderBy(x => x.Account),
        };
}

public sealed class AccountStatementReport(
    ReportExecutor executor, AppDbContext db, ILedgerQueryService ledger)
    : ReportRunner<CashBankReportRow>(executor, db)
{
    protected override ReportDefinition<CashBankReportRow> Definition { get; } = new()
    {
        Key = "account-statement",
        Title = "Account Statement",
        Supported = ReportFilters.Account,
        Columns =
        [
            new ReportColumn("account", "Account"),
            new ReportColumn("opening", "Opening Balance", Numeric: true, Total: "opening"),
            new ReportColumn("credits", "Credits", Numeric: true, Total: "credits"),
            new ReportColumn("debits", "Debits", Numeric: true, Total: "debits"),
            new ReportColumn("closing", "Closing Balance", Numeric: true, Total: "closing"),
        ],
        DefaultSortBy = "account",
        SortKeys = CashBankSort.ByLabel,
        Aggregates =
        [
            new ReportAggregate<CashBankReportRow>("opening", x => x.Opening),
            new ReportAggregate<CashBankReportRow>("credits", x => x.Credits),
            new ReportAggregate<CashBankReportRow>("debits", x => x.Debits),
            new ReportAggregate<CashBankReportRow>("closing", x => x.Closing),
        ],
    };

    protected override async ValueTask<IQueryable<CashBankReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var accounts = await db.Accounts.AsNoTracking()
            .Select(a => new { a.Id, a.Name, a.OpeningBalance }).ToListAsync(ct);

        var rows = new List<CashBankReportRow>();
        foreach (var a in accounts)
        {
            var totals = await db.LedgerEntries.AsNoTracking()
                .Where(e => e.AccountId == a.Id)
                .GroupBy(e => 1)
                .Select(g => new { Credit = g.Sum(x => x.Credit), Debit = g.Sum(x => x.Debit) })
                .FirstOrDefaultAsync(ct);
            decimal credits = totals?.Credit ?? 0m;
            decimal debits = totals?.Debit ?? 0m;
            decimal closing = await ledger.GetAccountBalanceAsync(a.Id, ct);

            rows.Add(new CashBankReportRow
            {
                Id = a.Id,
                RowAccountId = a.Id,
                Account = a.Name,
                Opening = a.OpeningBalance,
                Credits = credits,
                Debits = debits,
                Closing = closing,
                RowSearchText = a.Name,
            });
        }

        return rows.AsQueryable();
    }
}

public sealed class PaymentModeReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<CashBankReportRow>(executor, db)
{
    protected override ReportDefinition<CashBankReportRow> Definition { get; } = new()
    {
        Key = "payment-mode-report",
        Title = "Payment Mode Report",
        Supported = ReportFilters.Date | ReportFilters.PaymentMode,
        Columns =
        [
            new ReportColumn("label", "Payment Mode"),
            new ReportColumn("received", "Received", Numeric: true, Total: "received"),
            new ReportColumn("paid", "Paid", Numeric: true, Total: "paid"),
        ],
        DefaultSortBy = "label",
        SortKeys = CashBankSort.ByLabel,
        Aggregates =
        [
            new ReportAggregate<CashBankReportRow>("received", x => x.Received),
            new ReportAggregate<CashBankReportRow>("paid", x => x.Paid),
        ],
    };

    protected override async ValueTask<IQueryable<CashBankReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var modes = await db.PaymentModes.AsNoTracking().Select(m => new { m.Id, m.Name }).ToListAsync(ct);
        var byMode = await db.Settlements.AsNoTracking()
            .Where(s => s.Status == SettlementStatus.Active)
            .GroupBy(s => new { s.PaymentModeId, s.Direction })
            .Select(g => new { g.Key.PaymentModeId, g.Key.Direction, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        return modes.Select(m => new CashBankReportRow
        {
            Id = m.Id,
            RowPaymentModeId = m.Id,
            Label = m.Name,
            Received = byMode.Where(x => x.PaymentModeId == m.Id && x.Direction == SettlementDirection.In).Sum(x => x.Amount),
            Paid = byMode.Where(x => x.PaymentModeId == m.Id && x.Direction == SettlementDirection.Out).Sum(x => x.Amount),
            RowSearchText = m.Name,
        }).AsQueryable();
    }
}

public sealed class BankWiseReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<CashBankReportRow>(executor, db)
{
    protected override ReportDefinition<CashBankReportRow> Definition { get; } = new()
    {
        Key = "bank-wise-report",
        Title = "Bank-wise Report",
        Supported = ReportFilters.Date | ReportFilters.Account | ReportFilters.ReconciliationStatus | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("account", "Account"),
            new ReportColumn("narration", "Narration"),
            new ReportColumn("debits", "Debit", Numeric: true, Total: "debits"),
            new ReportColumn("credits", "Credit", Numeric: true, Total: "credits"),
            new ReportColumn("status", "Status"),
        ],
        DefaultSortBy = "date",
        DefaultSortDescending = true,
        SortKeys = CashBankSort.ByDate,
        Aggregates =
        [
            new ReportAggregate<CashBankReportRow>("credits", x => x.Credits),
            new ReportAggregate<CashBankReportRow>("debits", x => x.Debits),
        ],
    };

    protected override ValueTask<IQueryable<CashBankReportRow>> SourceAsync(AppDbContext db, CancellationToken ct) =>
        ValueTask.FromResult(
            from t in db.BankTransactions.AsNoTracking()
            join a in db.Accounts.AsNoTracking() on t.AccountId equals a.Id
            select new CashBankReportRow
            {
                Id = t.Id,
                Date = t.ValueDate,
                Account = a.Name,
                Narration = t.Narration,
                Credits = t.Credit,
                Debits = t.Debit,
                Status = t.Status.ToString(),
                RowDate = t.ValueDate,
                RowAccountId = t.AccountId,
                RowReconciliationStatus = t.Status.ToString(),
                RowSearchText = t.Narration,
            });
}

public sealed class BankReconciliationReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<CashBankReportRow>(executor, db)
{
    protected override ReportDefinition<CashBankReportRow> Definition { get; } = new()
    {
        Key = "bank-reconciliation-report",
        Title = "Bank Reconciliation Report",
        Supported = ReportFilters.Account,
        Columns =
        [
            new ReportColumn("account", "Account"),
            new ReportColumn("status", "Status"),
            new ReportColumn("count", "Count", Numeric: true, Total: "count"),
            new ReportColumn("amount", "Amount", Numeric: true, Total: "amount"),
        ],
        DefaultSortBy = "account",
        SortKeys = CashBankSort.ByLabel,
        Aggregates =
        [
            new ReportAggregate<CashBankReportRow>("amount", x => x.Amount),
            new ReportAggregate<CashBankReportRow>("count", x => x.Count),
        ],
    };

    protected override async ValueTask<IQueryable<CashBankReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var data = await (
            from t in db.BankTransactions.AsNoTracking()
            join a in db.Accounts.AsNoTracking() on t.AccountId equals a.Id
            select new { a.Id, a.Name, t.Status, Amount = t.Credit + t.Debit }).ToListAsync(ct);

        return data
            .GroupBy(x => (x.Id, x.Name, Reconciled: x.Status == BankTransactionStatus.Reconciled))
            .Select(g => new CashBankReportRow
            {
                RowAccountId = g.Key.Id,
                Account = g.Key.Name,
                Status = g.Key.Reconciled ? "Reconciled" : "Unreconciled",
                Count = g.Count(),
                Amount = g.Sum(x => x.Amount),
                RowSearchText = g.Key.Name,
            }).AsQueryable();
    }
}

public sealed class PendingReconciliationReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<CashBankReportRow>(executor, db)
{
    protected override ReportDefinition<CashBankReportRow> Definition { get; } = new()
    {
        Key = "pending-reconciliation-report",
        Title = "Pending Reconciliation Report",
        Supported = ReportFilters.Date | ReportFilters.Account | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("account", "Account"),
            new ReportColumn("narration", "Narration"),
            new ReportColumn("status", "Status"),
            new ReportColumn("amount", "Amount", Numeric: true, Total: "amount"),
        ],
        DefaultSortBy = "date",
        SortKeys = CashBankSort.ByDate,
        Aggregates = [new ReportAggregate<CashBankReportRow>("amount", x => x.Amount)],
    };

    protected override ValueTask<IQueryable<CashBankReportRow>> SourceAsync(AppDbContext db, CancellationToken ct) =>
        ValueTask.FromResult(
            from t in db.BankTransactions.AsNoTracking()
            join a in db.Accounts.AsNoTracking() on t.AccountId equals a.Id
            where t.Status == BankTransactionStatus.Pending || t.Status == BankTransactionStatus.InReview
            select new CashBankReportRow
            {
                Id = t.Id,
                Date = t.ValueDate,
                Account = a.Name,
                Narration = t.Narration,
                Status = t.Status.ToString(),
                Amount = t.Credit + t.Debit,
                RowDate = t.ValueDate,
                RowAccountId = t.AccountId,
                RowSearchText = t.Narration,
            });
}

public sealed class ExcludedTransactionReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<CashBankReportRow>(executor, db)
{
    protected override ReportDefinition<CashBankReportRow> Definition { get; } = new()
    {
        Key = "excluded-transaction-report",
        Title = "Excluded Transaction Report",
        Supported = ReportFilters.Date | ReportFilters.Account | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("account", "Account"),
            new ReportColumn("narration", "Narration"),
            new ReportColumn("reason", "Reason"),
            new ReportColumn("user", "Excluded By"),
            new ReportColumn("amount", "Amount", Numeric: true, Total: "amount"),
        ],
        DefaultSortBy = "date",
        DefaultSortDescending = true,
        SortKeys = CashBankSort.ByDate,
        Aggregates = [new ReportAggregate<CashBankReportRow>("amount", x => x.Amount)],
    };

    protected override async ValueTask<IQueryable<CashBankReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var txns = await (
            from t in db.BankTransactions.AsNoTracking()
            join a in db.Accounts.AsNoTracking() on t.AccountId equals a.Id
            where t.Status == BankTransactionStatus.Excluded
            select new { t.Id, t.ValueDate, Account = a.Name, t.Narration, t.ExclusionReason, t.AccountId, Amount = t.Credit + t.Debit })
            .ToListAsync(ct);

        var excludeAudits = await db.AuditLogs.AsNoTracking()
            .Where(x => x.Module == "bank_reconciliation" && x.Action == "exclude")
            .Select(x => new { x.RecordId, x.UserId })
            .ToListAsync(ct);
        var userById = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var userByTx = excludeAudits
            .GroupBy(x => x.RecordId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.UserId).LastOrDefault());

        return txns.Select(t => new CashBankReportRow
        {
            Id = t.Id,
            Date = t.ValueDate,
            Account = t.Account,
            Narration = t.Narration,
            Reason = t.ExclusionReason,
            User = userByTx.TryGetValue(t.Id.ToString(), out long? uid) && uid is { } id
                ? userById.GetValueOrDefault(id, $"User {id}")
                : null,
            Amount = t.Amount,
            RowDate = t.ValueDate,
            RowAccountId = t.AccountId,
            RowSearchText = t.Narration + " " + t.ExclusionReason,
        }).AsQueryable();
    }
}

public sealed class BankReconciliationExceptionsReport(
    ReportExecutor executor, AppDbContext db, IMatchSuggestionService suggestions)
    : ReportRunner<CashBankReportRow>(executor, db)
{
    protected override ReportDefinition<CashBankReportRow> Definition { get; } = new()
    {
        Key = "bank-reconciliation-exceptions",
        Title = "Bank Reconciliation Exceptions",
        Supported = ReportFilters.Account | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("account", "Account"),
            new ReportColumn("narration", "Narration"),
            new ReportColumn("label", "Exception"),
            new ReportColumn("amount", "Bank Amount", Numeric: true),
            new ReportColumn("allocated", "Allocated", Numeric: true),
            new ReportColumn("difference", "Difference", Numeric: true, Total: "difference"),
        ],
        DefaultSortBy = "date",
        SortKeys = CashBankSort.ByDate,
        Aggregates = [new ReportAggregate<CashBankReportRow>("difference", x => x.Difference)],
    };

    protected override async ValueTask<IQueryable<CashBankReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var txns = await (
            from t in db.BankTransactions.AsNoTracking()
            join a in db.Accounts.AsNoTracking() on t.AccountId equals a.Id
            where t.Status != BankTransactionStatus.Excluded
            select new { t.Id, t.ValueDate, Account = a.Name, t.Narration, t.AccountId, t.Status, Amount = t.Credit + t.Debit })
            .ToListAsync(ct);

        // A bank transaction can carry several active links (a split map across a vendor
        // and/or Personal/Office/Savings — client request, 2026-09-04), so sum rather than
        // assume exactly one link per transaction.
        var settlementLinks = await db.ReconciliationLinks.AsNoTracking()
            .Where(l => l.UnlinkedAtUtc == null
                && (l.Kind == ReconciliationLinkKind.Settlement || l.Kind == ReconciliationLinkKind.CustomWorkPayment))
            .Join(db.Settlements.AsNoTracking(), l => l.SettlementId!.Value, s => s.Id,
                (l, s) => new { l.BankTransactionId, Amount = s.Amount })
            .ToListAsync(ct);
        var commonExpenseLinks = await db.ReconciliationLinks.AsNoTracking()
            .Where(l => l.UnlinkedAtUtc == null && l.Kind == ReconciliationLinkKind.CommonExpense)
            .Join(db.Set<ColourBricks.Domain.CommonExpenses.CommonExpense>().AsNoTracking(),
                l => l.CommonExpenseId!.Value, e => e.Id,
                (l, e) => new { l.BankTransactionId, Amount = e.Amount })
            .ToListAsync(ct);
        var directExpenseLinks = await db.ReconciliationLinks.AsNoTracking()
            .Where(l => l.UnlinkedAtUtc == null && l.Kind == ReconciliationLinkKind.DirectExpense)
            .Join(db.Obligations.AsNoTracking(), l => l.ObligationId!.Value, o => o.Id,
                (l, o) => new { l.BankTransactionId, Amount = o.Amount })
            .ToListAsync(ct);
        var allocationByTx = settlementLinks.Concat(commonExpenseLinks).Concat(directExpenseLinks)
            .GroupBy(x => x.BankTransactionId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var rows = new List<CashBankReportRow>();
        long serial = 0;
        foreach (var t in txns)
        {
            if (t.Status == BankTransactionStatus.Reconciled)
            {
                decimal allocated = allocationByTx.GetValueOrDefault(t.Id, 0m);
                if (Math.Abs(allocated - t.Amount) > Money.Tolerance)
                {
                    rows.Add(Exception(++serial, t.Id, t.ValueDate, t.Account, t.Narration, t.AccountId,
                        "Bank amount ≠ allocation", t.Amount, allocated));
                }
            }
            else if (t.Status is BankTransactionStatus.Pending or BankTransactionStatus.InReview)
            {
                MatchSuggestionsDto s = await suggestions.SuggestAsync(t.Id, ct);
                if (s.Suggestions.Count == 0)
                {
                    rows.Add(Exception(++serial, t.Id, t.ValueDate, t.Account, t.Narration, t.AccountId,
                        "No suggested match", t.Amount, 0m));
                }
            }
        }

        return rows.AsQueryable();
    }

    private static CashBankReportRow Exception(
        long serial, long txId, DateOnly date, string account, string narration, long accountId,
        string kind, decimal amount, decimal allocated) => new()
    {
        Id = serial,
        Date = date,
        Account = account,
        Narration = narration,
        Label = kind,
        Amount = amount,
        Allocated = allocated,
        Difference = amount - allocated,
        RowDate = date,
        RowAccountId = accountId,
        RowSearchText = narration + " " + kind,
    };
}

public sealed class ReconciliationControlReport(
    ReportExecutor executor, AppDbContext db, IIntegrityCheckService integrity)
    : ReportRunner<CashBankReportRow>(executor, db)
{
    protected override ReportDefinition<CashBankReportRow> Definition { get; } = new()
    {
        Key = "reconciliation-control-report",
        Title = "Reconciliation Control Report",
        Supported = ReportFilters.None,
        Columns =
        [
            new ReportColumn("label", "Control"),
            new ReportColumn("narration", "Formula"),
            new ReportColumn("status", "Result"),
            new ReportColumn("count", "Violations", Numeric: true, Total: "count"),
        ],
        DefaultSortBy = "label",
        SortKeys = CashBankSort.ByLabel,
        Aggregates = [new ReportAggregate<CashBankReportRow>("count", x => x.Count)],
    };

    protected override async ValueTask<IQueryable<CashBankReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        IntegrityCheckReport report = await integrity.RunAsync(ct);
        long serial = 0;
        return report.Controls.Select(control => new CashBankReportRow
        {
            Id = ++serial,
            Label = control.Control,
            Narration = control.Formula,
            Status = control.Passed ? "Passed" : "Failed",
            Passed = control.Passed,
            Count = control.Violations.Count,
            RowSearchText = control.Control,
        }).AsQueryable();
    }
}
