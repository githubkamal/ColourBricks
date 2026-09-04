using ColourBricks.Application.Loans;
using ColourBricks.Application.Reporting;
using ColourBricks.Application.Reporting.Framework;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Reporting.Framework;

/// <summary>P8-T06 — the BRD §56 company-level reports and analytics.</summary>
public sealed class CompanyReportRow : IReportRow
{
    public long? Id { get; init; }
    public string? Metric { get; init; }
    public string? Month { get; init; }
    public string? Project { get; init; }
    public string? Bucket { get; init; }
    public decimal Value { get; init; }
    public decimal Income { get; init; }
    public decimal Expense { get; init; }
    public decimal Profit { get; init; }
    public decimal Revenue { get; init; }
    public decimal ActualCost { get; init; }
    public decimal Amount { get; init; }

    public DateOnly? RowDate => null;
    public long? RowProjectId { get; init; }
    public long? RowPartyId => null;
    public long? RowDepartmentId => null;
    public long? RowItemId => null;
    public long? RowCategoryId => null;
    public long? RowPaymentModeId => null;
    public long? RowAccountId => null;
    public string? RowPaymentStatus => null;
    public string? RowTransactionType => null;
    public string? RowReconciliationStatus => null;
    public string? RowSearchText { get; init; }
}

internal static class CompanySort
{
    public static readonly IReadOnlyDictionary<string, Func<IQueryable<CompanyReportRow>, bool, IOrderedQueryable<CompanyReportRow>>> ByMonth =
        new Dictionary<string, Func<IQueryable<CompanyReportRow>, bool, IOrderedQueryable<CompanyReportRow>>>
        {
            ["month"] = (q, d) => d ? q.OrderByDescending(x => x.Month) : q.OrderBy(x => x.Month),
        };

    public static readonly IReadOnlyDictionary<string, Func<IQueryable<CompanyReportRow>, bool, IOrderedQueryable<CompanyReportRow>>> ByProfit =
        new Dictionary<string, Func<IQueryable<CompanyReportRow>, bool, IOrderedQueryable<CompanyReportRow>>>
        {
            ["profit"] = (q, d) => d ? q.OrderByDescending(x => x.Profit) : q.OrderBy(x => x.Profit),
            ["project"] = (q, d) => d ? q.OrderByDescending(x => x.Project) : q.OrderBy(x => x.Project),
        };
}

public sealed class CompanySummaryReport(ReportExecutor executor, AppDbContext db, IReportingService reporting)
    : ReportRunner<CompanyReportRow>(executor, db)
{
    protected override ReportDefinition<CompanyReportRow> Definition { get; } = new()
    {
        Key = "company-summary",
        Title = "Company Summary",
        Supported = ReportFilters.None,
        Columns =
        [
            new ReportColumn("metric", "Metric"),
            new ReportColumn("value", "Value", Numeric: true),
        ],
        DefaultSortBy = "metric",
        SortKeys = new Dictionary<string, Func<IQueryable<CompanyReportRow>, bool, IOrderedQueryable<CompanyReportRow>>>
        {
            ["metric"] = (q, d) => d ? q.OrderByDescending(x => x.Id) : q.OrderBy(x => x.Id),
        },
        Aggregates = [],
    };

    protected override async ValueTask<IQueryable<CompanyReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        CompanyDashboardDto d = await reporting.CompanyDashboardAsync(ct);
        long serial = 0;
        return d.Tiles.Select(t => new CompanyReportRow
        {
            Id = ++serial,
            Metric = t.Key,
            Value = t.Value,
            RowSearchText = t.Key + " " + t.Label,
        }).AsQueryable();
    }
}

public sealed class CompanyMonthlyReport(ReportExecutor executor, AppDbContext db, IReportingService reporting)
    : ReportRunner<CompanyReportRow>(executor, db)
{
    protected override ReportDefinition<CompanyReportRow> Definition { get; } = new()
    {
        Key = "company-monthly",
        Title = "Monthly Income vs Expense",
        Supported = ReportFilters.None,
        Columns =
        [
            new ReportColumn("month", "Month"),
            new ReportColumn("income", "Income", Numeric: true, Total: "income"),
            new ReportColumn("expense", "Expense", Numeric: true, Total: "expense"),
            new ReportColumn("profit", "Profit / Loss", Numeric: true, Total: "profit"),
        ],
        DefaultSortBy = "month",
        SortKeys = CompanySort.ByMonth,
        Aggregates =
        [
            new ReportAggregate<CompanyReportRow>("income", x => x.Income),
            new ReportAggregate<CompanyReportRow>("expense", x => x.Expense),
            new ReportAggregate<CompanyReportRow>("profit", x => x.Profit),
        ],
    };

    protected override async ValueTask<IQueryable<CompanyReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        CompanyDashboardDto d = await reporting.CompanyDashboardAsync(ct);
        long serial = 0;
        return d.MonthlyFlow.Select(m => new CompanyReportRow
        {
            Id = ++serial,
            Month = m.Month,
            Income = m.Income,
            Expense = m.Expense,
            Profit = m.Income - m.Expense,
            RowSearchText = m.Month,
        }).AsQueryable();
    }
}

public sealed class ProjectProfitabilityRankingReport(
    ReportExecutor executor, AppDbContext db, IReportingService reporting)
    : ReportRunner<CompanyReportRow>(executor, db)
{
    protected override ReportDefinition<CompanyReportRow> Definition { get; } = new()
    {
        Key = "project-profitability-ranking",
        Title = "Project-wise Profitability",
        Supported = ReportFilters.Project,
        Columns =
        [
            new ReportColumn("project", "Project"),
            new ReportColumn("revenue", "Revenue", Numeric: true, Total: "revenue"),
            new ReportColumn("actualCost", "Actual Cost", Numeric: true, Total: "actualCost"),
            new ReportColumn("profit", "Profit / Loss", Numeric: true, Total: "profit"),
        ],
        DefaultSortBy = "profit",
        DefaultSortDescending = true,
        SortKeys = CompanySort.ByProfit,
        Aggregates =
        [
            new ReportAggregate<CompanyReportRow>("revenue", x => x.Revenue),
            new ReportAggregate<CompanyReportRow>("actualCost", x => x.ActualCost),
            new ReportAggregate<CompanyReportRow>("profit", x => x.Profit),
        ],
    };

    protected override async ValueTask<IQueryable<CompanyReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        CompanyDashboardDto d = await reporting.CompanyDashboardAsync(ct);
        return d.ProjectProfitability.Select(p => new CompanyReportRow
        {
            Id = p.ProjectId,
            RowProjectId = p.ProjectId,
            Project = p.ProjectName,
            Revenue = p.Revenue,
            ActualCost = p.ActualCost,
            Profit = p.Profit,
            RowSearchText = p.ProjectName,
        }).AsQueryable();
    }
}

public sealed class CompanyPnlReport(ReportExecutor executor, AppDbContext db, IReportingService reporting)
    : ReportRunner<CompanyReportRow>(executor, db)
{
    protected override ReportDefinition<CompanyReportRow> Definition { get; } = new()
    {
        Key = "company-pnl",
        Title = "Company Profit / Loss",
        Supported = ReportFilters.Project,
        Columns =
        [
            new ReportColumn("project", "Project"),
            new ReportColumn("revenue", "Revenue", Numeric: true, Total: "revenue"),
            new ReportColumn("actualCost", "Actual Cost", Numeric: true, Total: "actualCost"),
            new ReportColumn("profit", "Profit / Loss", Numeric: true, Total: "profit"),
        ],
        DefaultSortBy = "project",
        SortKeys = CompanySort.ByProfit,
        Aggregates =
        [
            new ReportAggregate<CompanyReportRow>("revenue", x => x.Revenue),
            new ReportAggregate<CompanyReportRow>("actualCost", x => x.ActualCost),
            new ReportAggregate<CompanyReportRow>("profit", x => x.Profit),
        ],
    };

    protected override async ValueTask<IQueryable<CompanyReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        CompanyPnlDto pnl = await reporting.CompanyPnlAsync("Receipts", ct);
        return pnl.Projects.Select(p => new CompanyReportRow
        {
            Id = p.ProjectId,
            RowProjectId = p.ProjectId,
            Project = p.ProjectName,
            Revenue = p.Revenue,
            ActualCost = p.ActualCost,
            Profit = p.GrossProfit,
            RowSearchText = p.ProjectName,
        }).AsQueryable();
    }
}

public sealed class ExpenseCategoryAnalysisReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<CompanyReportRow>(executor, db)
{
    protected override ReportDefinition<CompanyReportRow> Definition { get; } = new()
    {
        Key = "expense-category-analysis",
        Title = "Expense Category Analysis",
        Supported = ReportFilters.Date,
        Columns =
        [
            new ReportColumn("bucket", "Category"),
            new ReportColumn("amount", "Amount", Numeric: true, Total: "amount"),
        ],
        DefaultSortBy = "amount",
        DefaultSortDescending = true,
        SortKeys = new Dictionary<string, Func<IQueryable<CompanyReportRow>, bool, IOrderedQueryable<CompanyReportRow>>>
        {
            ["amount"] = (q, d) => d ? q.OrderByDescending(x => x.Amount) : q.OrderBy(x => x.Amount),
            ["bucket"] = (q, d) => d ? q.OrderByDescending(x => x.Bucket) : q.OrderBy(x => x.Bucket),
        },
        Aggregates = [new ReportAggregate<CompanyReportRow>("amount", x => x.Amount)],
    };

    protected override async ValueTask<IQueryable<CompanyReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var byBucket = await (
            from e in db.LedgerEntries.AsNoTracking()
            join c in db.ExpenseCategories.AsNoTracking() on e.CategoryId equals c.Id
            where c.IsCost
            group new { e.Debit, e.Credit } by c.Bucket into g
            select new { Bucket = g.Key, Amount = g.Sum(x => x.Debit - x.Credit) }).ToListAsync(ct);

        long serial = 0;
        return byBucket.Select(b => new CompanyReportRow
        {
            Id = ++serial,
            Bucket = b.Bucket,
            Amount = b.Amount,
            RowSearchText = b.Bucket,
        }).AsQueryable();
    }
}

public sealed class CompanyOutstandingReport(
    ReportExecutor executor, AppDbContext db, IReportingService reporting, ILoanReportService loans)
    : ReportRunner<CompanyReportRow>(executor, db)
{
    protected override ReportDefinition<CompanyReportRow> Definition { get; } = new()
    {
        Key = "company-outstanding",
        Title = "Company Outstanding",
        Supported = ReportFilters.None,
        Columns =
        [
            new ReportColumn("metric", "Kind"),
            new ReportColumn("value", "Outstanding", Numeric: true, Total: "value"),
        ],
        DefaultSortBy = "metric",
        SortKeys = new Dictionary<string, Func<IQueryable<CompanyReportRow>, bool, IOrderedQueryable<CompanyReportRow>>>
        {
            ["metric"] = (q, d) => d ? q.OrderByDescending(x => x.Metric) : q.OrderBy(x => x.Metric),
        },
        Aggregates = [new ReportAggregate<CompanyReportRow>("value", x => x.Value)],
    };

    protected override async ValueTask<IQueryable<CompanyReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        CompanyDashboardDto d = await reporting.CompanyDashboardAsync(ct);
        decimal Tile(string key) => d.Tiles.FirstOrDefault(t => t.Key == key)?.Value ?? 0m;
        decimal loanOutstanding = (await loans.OutstandingAsync(null, ct)).Sum(l => l.OutstandingPrincipal);

        return new List<CompanyReportRow>
        {
            new() { Id = 1, Metric = "Vendor", Value = Tile("vendorOutstanding"), RowSearchText = "Vendor" },
            new() { Id = 2, Metric = "Subcontractor", Value = Tile("subcontractorOutstanding"), RowSearchText = "Subcontractor" },
            new() { Id = 3, Metric = "Loan", Value = loanOutstanding, RowSearchText = "Loan" },
        }.AsQueryable();
    }
}
