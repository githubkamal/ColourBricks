using ColourBricks.Application.Reporting.Framework;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Reporting.Framework;

/// <summary>
/// P8-T01 validation — a whole report built with nothing but the framework: a
/// definition file and a projection. No bespoke filter, sort or pagination code.
/// </summary>
public sealed class LedgerReportRow : IReportRow
{
    public long Id { get; init; }
    public DateOnly Date { get; init; }
    public string Category { get; init; } = "";
    public string Source { get; init; } = "";
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public bool IsReversal { get; init; }

    // Filter surface (real properties, set in the projection below).
    public DateOnly? RowDate { get; init; }
    public long? RowProjectId { get; init; }
    public long? RowPartyId { get; init; }
    public long? RowDepartmentId => null;
    public long? RowItemId => null;
    public long? RowCategoryId { get; init; }
    public long? RowPaymentModeId => null;
    public long? RowAccountId { get; init; }
    public string? RowPaymentStatus => null;
    public string? RowTransactionType { get; init; }
    public string? RowReconciliationStatus => null;
    public string? RowSearchText { get; init; }
}

public sealed class LedgerReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<LedgerReportRow>(executor, db)
{
    protected override ReportDefinition<LedgerReportRow> Definition { get; } = new()
    {
        Key = "ledger",
        Title = "Ledger",
        Supported = ReportFilters.Date | ReportFilters.Project | ReportFilters.Vendor
            | ReportFilters.Subcontractor | ReportFilters.Category | ReportFilters.Account
            | ReportFilters.TransactionType | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("category", "Category"),
            new ReportColumn("source", "Source"),
            new ReportColumn("debit", "Debit", Numeric: true, Total: "debit"),
            new ReportColumn("credit", "Credit", Numeric: true, Total: "credit"),
        ],
        DefaultSortBy = "date",
        DefaultSortDescending = true,
        SortKeys = new Dictionary<string, Func<IQueryable<LedgerReportRow>, bool, IOrderedQueryable<LedgerReportRow>>>
        {
            ["date"] = (q, d) => d ? q.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
                                   : q.OrderBy(x => x.Date).ThenBy(x => x.Id),
            ["debit"] = (q, d) => d ? q.OrderByDescending(x => x.Debit) : q.OrderBy(x => x.Debit),
            ["credit"] = (q, d) => d ? q.OrderByDescending(x => x.Credit) : q.OrderBy(x => x.Credit),
        },
        Aggregates =
        [
            new ReportAggregate<LedgerReportRow>("debit", x => x.Debit),
            new ReportAggregate<LedgerReportRow>("credit", x => x.Credit),
        ],
    };

    protected override ValueTask<IQueryable<LedgerReportRow>> SourceAsync(AppDbContext db, CancellationToken ct) =>
        ValueTask.FromResult<IQueryable<LedgerReportRow>>(
        from e in db.LedgerEntries.AsNoTracking()
        join c in db.ExpenseCategories.AsNoTracking() on e.CategoryId equals c.Id
        select new LedgerReportRow
        {
            Id = e.Id,
            Date = e.EntryDate,
            Category = c.Name,
            Source = e.SourceType,
            Debit = e.Debit,
            Credit = e.Credit,
            IsReversal = e.IsReversal,
            RowDate = e.EntryDate,
            RowProjectId = e.ProjectId,
            RowPartyId = e.PartyId,
            RowCategoryId = e.CategoryId,
            RowAccountId = e.AccountId,
            RowTransactionType = e.SourceType,
            RowSearchText = c.Name + " " + e.SourceType,
        });
}
