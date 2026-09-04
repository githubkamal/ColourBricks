using ColourBricks.Application.Ledger;
using ColourBricks.Application.Loans;
using ColourBricks.Application.Outstanding;
using ColourBricks.Application.Reporting;
using ColourBricks.Application.Reporting.Framework;
using ColourBricks.Domain.Obligations;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Reporting.Framework;

// ── Project Material Report (EF-backed) ──────────────────────────────────────

public sealed class MaterialReportRow : IReportRow
{
    public long Id { get; init; }
    public DateOnly Date { get; init; }
    public string Item { get; init; } = "";
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = "";
    public decimal Rate { get; init; }
    public decimal Amount { get; init; }
    public string? Vendor { get; init; }

    public DateOnly? RowDate { get; init; }
    public long? RowProjectId { get; init; }
    public long? RowPartyId { get; init; }
    public long? RowDepartmentId => null;
    public long? RowItemId { get; init; }
    public long? RowCategoryId => null;
    public long? RowPaymentModeId => null;
    public long? RowAccountId => null;
    public string? RowPaymentStatus => null;
    public string? RowTransactionType => null;
    public string? RowReconciliationStatus => null;
    public string? RowSearchText { get; init; }
}

public sealed class ProjectMaterialReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<MaterialReportRow>(executor, db)
{
    protected override ReportDefinition<MaterialReportRow> Definition { get; } = new()
    {
        Key = "project-material",
        Title = "Project Material Report",
        Supported = ReportFilters.Date | ReportFilters.Project | ReportFilters.Vendor
            | ReportFilters.Item | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("item", "Item"),
            new ReportColumn("quantity", "Qty", Numeric: true, Total: "quantity"),
            new ReportColumn("rate", "Rate", Numeric: true),
            new ReportColumn("amount", "Amount", Numeric: true, Total: "amount"),
            new ReportColumn("vendor", "Vendor"),
        ],
        DefaultSortBy = "date",
        DefaultSortDescending = true,
        SortKeys = new Dictionary<string, Func<IQueryable<MaterialReportRow>, bool, IOrderedQueryable<MaterialReportRow>>>
        {
            ["date"] = (q, d) => d ? q.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
                                   : q.OrderBy(x => x.Date).ThenBy(x => x.Id),
            ["amount"] = (q, d) => d ? q.OrderByDescending(x => x.Amount) : q.OrderBy(x => x.Amount),
            ["item"] = (q, d) => d ? q.OrderByDescending(x => x.Item) : q.OrderBy(x => x.Item),
        },
        Aggregates =
        [
            new ReportAggregate<MaterialReportRow>("amount", x => x.Amount),
            new ReportAggregate<MaterialReportRow>("quantity", x => x.Quantity),
        ],
    };

    protected override ValueTask<IQueryable<MaterialReportRow>> SourceAsync(AppDbContext db, CancellationToken ct) =>
        ValueTask.FromResult(
            from l in db.ObligationLines.AsNoTracking()
            join o in db.Obligations.AsNoTracking() on l.ObligationId equals o.Id
            where o.Type == ObligationType.VendorPurchase && o.Status == ObligationStatus.Active
            select new MaterialReportRow
            {
                Id = l.Id,
                Date = o.Date,
                Item = l.ItemName,
                Quantity = l.Quantity,
                Unit = l.Unit,
                Rate = l.Rate,
                Amount = l.LineTotal,
                Vendor = db.Parties.Where(p => p.Id == o.PartyId).Select(p => p.Name).FirstOrDefault(),
                RowDate = o.Date,
                RowProjectId = o.ProjectId,
                RowPartyId = o.PartyId,
                RowItemId = l.ItemId,
                RowSearchText = l.ItemName,
            });
}

// ── Computed one-row-per-thing reports ──────────────────────────────────────

public sealed class SummaryReportRow : IReportRow
{
    public long? Id { get; init; }
    public string Label { get; init; } = "";
    public decimal ProjectValue { get; init; }
    public decimal EstimatedCost { get; init; }
    public decimal ActualCost { get; init; }
    public decimal Budget { get; init; }
    public decimal Actual { get; init; }
    public decimal Variance { get; init; }
    public decimal? VariancePercent { get; init; }
    public decimal Income { get; init; }
    public decimal Expenses { get; init; }
    public decimal ProfitLoss { get; init; }
    public decimal? ProfitPercent { get; init; }
    public decimal Outstanding { get; init; }

    public DateOnly? RowDate => null;
    public long? RowProjectId { get; init; }
    public long? RowPartyId => null;
    public long? RowDepartmentId => null;
    public long? RowItemId => null;
    public long? RowCategoryId { get; init; }
    public long? RowPaymentModeId => null;
    public long? RowAccountId => null;
    public string? RowPaymentStatus => null;
    public string? RowTransactionType => null;
    public string? RowReconciliationStatus => null;
    public string? RowSearchText { get; init; }
}

public sealed class ProjectFinancialSummaryReport(
    ReportExecutor executor, AppDbContext db, ILedgerQueryService ledger)
    : ReportRunner<SummaryReportRow>(executor, db)
{
    protected override ReportDefinition<SummaryReportRow> Definition { get; } = new()
    {
        Key = "project-financial-summary",
        Title = "Project Financial Summary",
        Supported = ReportFilters.Project,
        Columns =
        [
            new ReportColumn("label", "Project"),
            new ReportColumn("projectValue", "Project Value", Numeric: true, Total: "projectValue"),
            new ReportColumn("estimatedCost", "Estimated Cost", Numeric: true, Total: "estimatedCost"),
            new ReportColumn("actualCost", "Actual Cost", Numeric: true, Total: "actualCost"),
            new ReportColumn("income", "Income", Numeric: true, Total: "income"),
            new ReportColumn("expenses", "Expenses", Numeric: true, Total: "expenses"),
            new ReportColumn("profitLoss", "Profit / Loss", Numeric: true, Total: "profitLoss"),
        ],
        DefaultSortBy = "label",
        SortKeys = new Dictionary<string, Func<IQueryable<SummaryReportRow>, bool, IOrderedQueryable<SummaryReportRow>>>
        {
            ["label"] = (q, d) => d ? q.OrderByDescending(x => x.Label) : q.OrderBy(x => x.Label),
        },
        Aggregates =
        [
            new ReportAggregate<SummaryReportRow>("projectValue", x => x.ProjectValue),
            new ReportAggregate<SummaryReportRow>("estimatedCost", x => x.EstimatedCost),
            new ReportAggregate<SummaryReportRow>("actualCost", x => x.ActualCost),
            new ReportAggregate<SummaryReportRow>("income", x => x.Income),
            new ReportAggregate<SummaryReportRow>("expenses", x => x.Expenses),
            new ReportAggregate<SummaryReportRow>("profitLoss", x => x.ProfitLoss),
        ],
    };

    protected override async ValueTask<IQueryable<SummaryReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        List<(long Id, string Name, decimal Value, decimal Estimate)> projects = await db.Projects.AsNoTracking()
            .Select(p => new ValueTuple<long, string, decimal, decimal>(p.Id, p.Name, p.ContractValue, p.EstimatedCost))
            .ToListAsync(ct);

        var rows = new List<SummaryReportRow>(projects.Count);
        foreach ((long id, string name, decimal value, decimal estimate) in projects)
        {
            decimal actualCost = await ledger.GetProjectActualCostAsync(id, ct);
            decimal income = await ledger.GetProjectIncomeAsync(id, ct);
            rows.Add(new SummaryReportRow
            {
                Id = id,
                RowProjectId = id,
                Label = name,
                ProjectValue = value,
                EstimatedCost = estimate,
                ActualCost = actualCost,
                Income = income,
                Expenses = actualCost,
                ProfitLoss = income - actualCost,
                ProfitPercent = income == 0m ? null : Math.Round((income - actualCost) / income * 100m, 2),
                RowSearchText = name,
            });
        }

        return rows.AsQueryable();
    }
}

public sealed class ProjectBudgetVsActualReport(
    ReportExecutor executor, AppDbContext db, IReportingService reporting)
    : ReportRunner<SummaryReportRow>(executor, db)
{
    protected override ReportDefinition<SummaryReportRow> Definition { get; } = new()
    {
        Key = "project-budget-vs-actual",
        Title = "Budget vs Actual",
        Supported = ReportFilters.Project | ReportFilters.Category,
        Columns =
        [
            new ReportColumn("label", "Category"),
            new ReportColumn("budget", "Budget", Numeric: true, Total: "budget"),
            new ReportColumn("actual", "Actual", Numeric: true, Total: "actual"),
            new ReportColumn("variance", "Variance", Numeric: true, Total: "variance"),
        ],
        DefaultSortBy = "label",
        SortKeys = new Dictionary<string, Func<IQueryable<SummaryReportRow>, bool, IOrderedQueryable<SummaryReportRow>>>
        {
            ["label"] = (q, d) => d ? q.OrderByDescending(x => x.Label) : q.OrderBy(x => x.Label),
        },
        Aggregates =
        [
            new ReportAggregate<SummaryReportRow>("budget", x => x.Budget),
            new ReportAggregate<SummaryReportRow>("actual", x => x.Actual),
            new ReportAggregate<SummaryReportRow>("variance", x => x.Variance),
        ],
    };

    protected override async ValueTask<IQueryable<SummaryReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        List<long> projectIds = await db.Projects.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
        var rows = new List<SummaryReportRow>();
        foreach (long projectId in projectIds)
        {
            BudgetVsActualDto dto = await reporting.BudgetVsActualAsync(projectId, ct);
            foreach (BudgetVsActualRowDto row in dto.Rows)
            {
                rows.Add(new SummaryReportRow
                {
                    Id = row.CategoryId,
                    RowProjectId = projectId,
                    RowCategoryId = row.CategoryId,
                    Label = row.CategoryName,
                    Budget = row.Budget,
                    Actual = row.Actual,
                    Variance = row.Variance,
                    VariancePercent = row.VariancePercent,
                    RowSearchText = row.CategoryName,
                });
            }
        }

        return rows.AsQueryable();
    }
}

public sealed class ProjectOutstandingReport(
    ReportExecutor executor, AppDbContext db, IOutstandingService outstanding, ILoanReportService loans)
    : ReportRunner<SummaryReportRow>(executor, db)
{
    protected override ReportDefinition<SummaryReportRow> Definition { get; } = new()
    {
        Key = "project-outstanding",
        Title = "Project Outstanding",
        Supported = ReportFilters.Project,
        Columns =
        [
            new ReportColumn("label", "Kind"),
            new ReportColumn("outstanding", "Outstanding", Numeric: true, Total: "outstanding"),
        ],
        DefaultSortBy = "label",
        SortKeys = new Dictionary<string, Func<IQueryable<SummaryReportRow>, bool, IOrderedQueryable<SummaryReportRow>>>
        {
            ["label"] = (q, d) => d ? q.OrderByDescending(x => x.Label) : q.OrderBy(x => x.Label),
        },
        Aggregates =
        [
            new ReportAggregate<SummaryReportRow>("outstanding", x => x.Outstanding),
            new ReportAggregate<SummaryReportRow>("payableOnly", x => x.Label == "Loan" ? 0m : x.Outstanding),
        ],
    };

    protected override async ValueTask<IQueryable<SummaryReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        List<long> projectIds = await db.Projects.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
        IReadOnlyList<LoanOutstandingReportRowDto> loanRows = await loans.OutstandingAsync(null, ct);

        var rows = new List<SummaryReportRow>();
        foreach (long projectId in projectIds)
        {
            ProjectOutstandingSummaryDto s = await outstanding.ProjectSummaryAsync(projectId, ct);
            decimal loanOutstanding = loanRows.Where(l => l.ProjectId == projectId).Sum(l => l.OutstandingPrincipal);

            foreach ((string kind, decimal amount) in new[]
                     {
                         ("Vendor", s.VendorPayable),
                         ("Subcontractor", s.SubcontractorPayable),
                         ("Custom Work", s.CustomWorkPayable),
                         ("Donation", s.DonationPayable),
                         ("Loan", loanOutstanding),
                     })
            {
                rows.Add(new SummaryReportRow
                {
                    RowProjectId = projectId,
                    Label = kind,
                    Outstanding = amount,
                    RowSearchText = kind,
                });
            }
        }

        return rows.AsQueryable();
    }
}
