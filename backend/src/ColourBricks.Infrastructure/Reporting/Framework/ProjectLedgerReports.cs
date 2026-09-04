using ColourBricks.Application.Reporting.Framework;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Reporting.Framework;

/// <summary>
/// P8-T02 — one row per ledger entry for a project, joined to its category, party
/// and department. Drives the Project Expense Report, Project Ledger and Project
/// Labour Report (BRD §49): same projection, different filters and totals.
/// </summary>
public sealed class ProjectLedgerReportRow : IReportRow
{
    public long Id { get; init; }
    public DateOnly Date { get; init; }
    public string Category { get; init; } = "";
    public string Bucket { get; init; } = "";
    public string? Party { get; init; }
    public string? Department { get; init; }
    public string Source { get; init; } = "";
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public bool IsCost { get; init; }
    public bool IsIncome { get; init; }

    public DateOnly? RowDate { get; init; }
    public long? RowProjectId { get; init; }
    public long? RowPartyId { get; init; }
    public long? RowDepartmentId { get; init; }
    public long? RowItemId => null;
    public long? RowCategoryId { get; init; }
    public long? RowPaymentModeId => null;
    public long? RowAccountId { get; init; }
    public string? RowPaymentStatus => null;
    public string? RowTransactionType { get; init; }
    public string? RowReconciliationStatus => null;
    public string? RowSearchText { get; init; }
}

internal static class ProjectLedgerSource
{
    // Obligation-sourced ledger entries carry the obligation id as SourceId, so their
    // department can be looked up. Other entries keep a null department.
    private static readonly List<string> ObligationSources =
        ["SubcontractorWork", "VendorPurchase", "CustomWork", "DirectExpense"];

    public static IQueryable<ProjectLedgerReportRow> Build(AppDbContext db) =>
        from e in db.LedgerEntries.AsNoTracking()
        join c in db.ExpenseCategories.AsNoTracking() on e.CategoryId equals c.Id
        join ob in db.Obligations.AsNoTracking() on e.SourceId equals ob.Id into obs
        from ob in obs.DefaultIfEmpty()
        where e.ProjectId != null
        let fromObligation = ob != null && ObligationSources.Contains(e.SourceType)
        let departmentId = fromObligation ? ob.DepartmentId : null
        let partyId = e.PartyId ?? (fromObligation ? ob.PartyId : null)
        select new ProjectLedgerReportRow
        {
            Id = e.Id,
            Date = e.EntryDate,
            Category = c.Name,
            Bucket = c.Bucket,
            Party = db.Parties.Where(p => p.Id == partyId).Select(p => p.Name).FirstOrDefault(),
            Department = db.Departments.Where(d => d.Id == departmentId).Select(d => d.Name).FirstOrDefault(),
            Source = e.SourceType,
            Debit = e.Debit,
            Credit = e.Credit,
            IsCost = c.IsCost,
            IsIncome = c.Bucket == "Income",
            RowDate = e.EntryDate,
            RowProjectId = e.ProjectId,
            RowPartyId = partyId,
            RowDepartmentId = departmentId,
            RowCategoryId = e.CategoryId,
            RowAccountId = e.AccountId,
            RowTransactionType = e.SourceType,
            RowSearchText = c.Name + " " + e.SourceType,
        };
}

public sealed class ProjectExpenseReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<ProjectLedgerReportRow>(executor, db)
{
    protected override ReportDefinition<ProjectLedgerReportRow> Definition { get; } = new()
    {
        Key = "project-expense",
        Title = "Project Expense Report",
        Supported = ReportFilters.Date | ReportFilters.Project | ReportFilters.Category
            | ReportFilters.Department | ReportFilters.Vendor | ReportFilters.Subcontractor
            | ReportFilters.PaymentMode | ReportFilters.Account | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("category", "Category"),
            new ReportColumn("party", "Party"),
            new ReportColumn("source", "Source"),
            new ReportColumn("debit", "Debit", Numeric: true, Total: "debit"),
            new ReportColumn("credit", "Credit", Numeric: true, Total: "credit"),
        ],
        DefaultSortBy = "date",
        DefaultSortDescending = true,
        SortKeys = ProjectLedgerSortKeys.All,
        Aggregates =
        [
            new ReportAggregate<ProjectLedgerReportRow>("debit", x => x.Debit),
            new ReportAggregate<ProjectLedgerReportRow>("credit", x => x.Credit),
        ],
    };

    protected override ValueTask<IQueryable<ProjectLedgerReportRow>> SourceAsync(AppDbContext db, CancellationToken ct) =>
        ValueTask.FromResult(ProjectLedgerSource.Build(db).Where(x => x.IsCost));
}

public sealed class ProjectLedgerReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<ProjectLedgerReportRow>(executor, db)
{
    protected override ReportDefinition<ProjectLedgerReportRow> Definition { get; } = new()
    {
        Key = "project-ledger",
        Title = "Project Ledger",
        Supported = ReportFilters.Date | ReportFilters.Project | ReportFilters.Category
            | ReportFilters.TransactionType | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("category", "Category"),
            new ReportColumn("bucket", "Bucket"),
            new ReportColumn("source", "Source"),
            new ReportColumn("debit", "Debit", Numeric: true, Total: "debit"),
            new ReportColumn("credit", "Credit", Numeric: true, Total: "credit"),
        ],
        DefaultSortBy = "date",
        SortKeys = ProjectLedgerSortKeys.All,
        Aggregates =
        [
            new ReportAggregate<ProjectLedgerReportRow>("debit", x => x.Debit),
            new ReportAggregate<ProjectLedgerReportRow>("credit", x => x.Credit),
            new ReportAggregate<ProjectLedgerReportRow>("costNet", x => x.IsCost ? x.Debit - x.Credit : 0m),
            new ReportAggregate<ProjectLedgerReportRow>("incomeNet", x => x.IsIncome ? x.Debit - x.Credit : 0m),
        ],
    };

    protected override ValueTask<IQueryable<ProjectLedgerReportRow>> SourceAsync(AppDbContext db, CancellationToken ct) =>
        ValueTask.FromResult(ProjectLedgerSource.Build(db));
}

public sealed class ProjectLabourReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<ProjectLedgerReportRow>(executor, db)
{
    protected override ReportDefinition<ProjectLedgerReportRow> Definition { get; } = new()
    {
        Key = "project-labour",
        Title = "Project Labour Report",
        Supported = ReportFilters.Date | ReportFilters.Project | ReportFilters.Department | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("category", "Category"),
            new ReportColumn("party", "Team / Party"),
            new ReportColumn("debit", "Cost", Numeric: true, Total: "amount"),
        ],
        DefaultSortBy = "date",
        SortKeys = ProjectLedgerSortKeys.All,
        Aggregates =
        [
            new ReportAggregate<ProjectLedgerReportRow>("amount", x => x.Debit - x.Credit),
        ],
    };

    protected override ValueTask<IQueryable<ProjectLedgerReportRow>> SourceAsync(AppDbContext db, CancellationToken ct) =>
        ValueTask.FromResult(ProjectLedgerSource.Build(db).Where(x => x.Bucket == "Labour"));
}

internal static class ProjectLedgerSortKeys
{
    public static readonly IReadOnlyDictionary<string, Func<IQueryable<ProjectLedgerReportRow>, bool, IOrderedQueryable<ProjectLedgerReportRow>>> All =
        new Dictionary<string, Func<IQueryable<ProjectLedgerReportRow>, bool, IOrderedQueryable<ProjectLedgerReportRow>>>
        {
            ["date"] = (q, d) => d ? q.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
                                   : q.OrderBy(x => x.Date).ThenBy(x => x.Id),
            ["debit"] = (q, d) => d ? q.OrderByDescending(x => x.Debit) : q.OrderBy(x => x.Debit),
            ["credit"] = (q, d) => d ? q.OrderByDescending(x => x.Credit) : q.OrderBy(x => x.Credit),
            ["category"] = (q, d) => d ? q.OrderByDescending(x => x.Category) : q.OrderBy(x => x.Category),
        };
}
