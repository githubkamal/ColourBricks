using ColourBricks.Application.Donations;
using ColourBricks.Application.Reporting.Framework;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Reporting.Framework;

/// <summary>P8-T04 — the seven BRD §53 temple-donation reports.</summary>
public sealed class DonationReportRow : IReportRow
{
    public long? Id { get; init; }
    public DateOnly? Date { get; init; }
    public string? Project { get; init; }
    public string? Temple { get; init; }
    public decimal? Percentage { get; init; }
    public decimal ContractValue { get; init; }
    public decimal Allocated { get; init; }
    public decimal Paid { get; init; }
    public decimal Outstanding { get; init; }
    public string? Reference { get; init; }

    public DateOnly? RowDate { get; init; }
    public long? RowProjectId { get; init; }
    public long? RowPartyId { get; init; }
    public long? RowDepartmentId => null;
    public long? RowItemId => null;
    public long? RowCategoryId => null;
    public long? RowPaymentModeId { get; init; }
    public long? RowAccountId { get; init; }
    public string? RowPaymentStatus => null;
    public string? RowTransactionType => null;
    public string? RowReconciliationStatus => null;
    public string? RowSearchText { get; init; }
}

/// <summary>Base for the reports built from the per-temple donation outstanding rows.</summary>
public abstract class DonationReportBase(ReportExecutor executor, AppDbContext db, IDonationPaymentService donations)
    : ReportRunner<DonationReportRow>(executor, db)
{
    protected async Task<List<(long ProjectId, string Project, decimal ContractValue, decimal? Percentage,
        long TempleId, string Temple, decimal Allocated, decimal Paid, decimal Outstanding)>> LoadAsync(
        AppDbContext db, CancellationToken ct)
    {
        var setups = await db.ProjectDonations.AsNoTracking()
            .Select(pd => new { pd.ProjectId, pd.Percentage, pd.ContractValueSnapshot })
            .ToListAsync(ct);
        var projectNames = await db.Projects.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var result = new List<(long, string, decimal, decimal?, long, string, decimal, decimal, decimal)>();
        foreach (var setup in setups)
        {
            foreach (DonationTempleOutstandingDto t in await donations.OutstandingByTempleAsync(setup.ProjectId, ct))
            {
                result.Add((
                    setup.ProjectId, projectNames.GetValueOrDefault(setup.ProjectId, ""),
                    setup.ContractValueSnapshot, setup.Percentage,
                    t.TempleId, t.TempleName, t.Allocated, t.Paid, t.Outstanding));
            }
        }

        return result;
    }
}

public sealed class DonationProjectWiseReport(ReportExecutor executor, AppDbContext db, IDonationPaymentService donations)
    : DonationReportBase(executor, db, donations)
{
    protected override ReportDefinition<DonationReportRow> Definition { get; } = new()
    {
        Key = "donation-project-wise",
        Title = "Project-wise Donation",
        Supported = ReportFilters.Project,
        Columns =
        [
            new ReportColumn("project", "Project"),
            new ReportColumn("allocated", "Allocated", Numeric: true, Total: "allocated"),
            new ReportColumn("paid", "Paid", Numeric: true, Total: "paid"),
            new ReportColumn("outstanding", "Outstanding", Numeric: true, Total: "outstanding"),
        ],
        DefaultSortBy = "project",
        SortKeys = DonationSortKeys.ByLabel,
        Aggregates = DonationAggregates.AllocPaidOut,
    };

    protected override async ValueTask<IQueryable<DonationReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var data = await LoadAsync(db, ct);
        return data.GroupBy(x => (x.ProjectId, x.Project)).Select(g => new DonationReportRow
        {
            Id = g.Key.ProjectId,
            RowProjectId = g.Key.ProjectId,
            Project = g.Key.Project,
            Allocated = g.Sum(x => x.Allocated),
            Paid = g.Sum(x => x.Paid),
            Outstanding = g.Sum(x => x.Outstanding),
            RowSearchText = g.Key.Project,
        }).AsQueryable();
    }
}

public sealed class DonationTempleWiseReport(ReportExecutor executor, AppDbContext db, IDonationPaymentService donations)
    : DonationReportBase(executor, db, donations)
{
    protected override ReportDefinition<DonationReportRow> Definition { get; } = new()
    {
        Key = "donation-temple-wise",
        Title = "Temple-wise Donation",
        Supported = ReportFilters.Search,
        Columns =
        [
            new ReportColumn("temple", "Temple"),
            new ReportColumn("allocated", "Allocated", Numeric: true, Total: "allocated"),
            new ReportColumn("paid", "Paid", Numeric: true, Total: "paid"),
            new ReportColumn("outstanding", "Outstanding", Numeric: true, Total: "outstanding"),
        ],
        DefaultSortBy = "temple",
        SortKeys = new Dictionary<string, Func<IQueryable<DonationReportRow>, bool, IOrderedQueryable<DonationReportRow>>>
        {
            ["temple"] = (q, d) => d ? q.OrderByDescending(x => x.Temple) : q.OrderBy(x => x.Temple),
        },
        Aggregates = DonationAggregates.AllocPaidOut,
    };

    protected override async ValueTask<IQueryable<DonationReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var data = await LoadAsync(db, ct);
        return data.GroupBy(x => (x.TempleId, x.Temple)).Select(g => new DonationReportRow
        {
            Id = g.Key.TempleId,
            RowPartyId = g.Key.TempleId,
            Temple = g.Key.Temple,
            Allocated = g.Sum(x => x.Allocated),
            Paid = g.Sum(x => x.Paid),
            Outstanding = g.Sum(x => x.Outstanding),
            RowSearchText = g.Key.Temple,
        }).AsQueryable();
    }
}

public sealed class DonationAllocatedReport(ReportExecutor executor, AppDbContext db, IDonationPaymentService donations)
    : DonationReportBase(executor, db, donations)
{
    protected override ReportDefinition<DonationReportRow> Definition { get; } = new()
    {
        Key = "donation-allocated",
        Title = "Donation Allocated",
        Supported = ReportFilters.Project | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("project", "Project"),
            new ReportColumn("temple", "Temple"),
            new ReportColumn("allocated", "Allocated", Numeric: true, Total: "allocated"),
        ],
        DefaultSortBy = "project",
        SortKeys = DonationSortKeys.ByLabel,
        Aggregates = [new ReportAggregate<DonationReportRow>("allocated", x => x.Allocated)],
    };

    protected override async ValueTask<IQueryable<DonationReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var data = await LoadAsync(db, ct);
        return data.Select(x => new DonationReportRow
        {
            RowProjectId = x.ProjectId,
            RowPartyId = x.TempleId,
            Project = x.Project,
            Temple = x.Temple,
            Allocated = x.Allocated,
            RowSearchText = x.Project + " " + x.Temple,
        }).AsQueryable();
    }
}

public sealed class DonationOutstandingReport(ReportExecutor executor, AppDbContext db, IDonationPaymentService donations)
    : DonationReportBase(executor, db, donations)
{
    protected override ReportDefinition<DonationReportRow> Definition { get; } = new()
    {
        Key = "donation-outstanding",
        Title = "Donation Outstanding",
        Supported = ReportFilters.Project | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("project", "Project"),
            new ReportColumn("temple", "Temple"),
            new ReportColumn("allocated", "Allocated", Numeric: true, Total: "allocated"),
            new ReportColumn("paid", "Paid", Numeric: true, Total: "paid"),
            new ReportColumn("outstanding", "Outstanding", Numeric: true, Total: "outstanding"),
        ],
        DefaultSortBy = "project",
        SortKeys = DonationSortKeys.ByLabel,
        Aggregates = DonationAggregates.AllocPaidOut,
    };

    protected override async ValueTask<IQueryable<DonationReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var data = await LoadAsync(db, ct);
        return data.Select(x => new DonationReportRow
        {
            RowProjectId = x.ProjectId,
            RowPartyId = x.TempleId,
            Project = x.Project,
            Temple = x.Temple,
            Allocated = x.Allocated,
            Paid = x.Paid,
            Outstanding = x.Outstanding,
            RowSearchText = x.Project + " " + x.Temple,
        }).AsQueryable();
    }
}

public sealed class DonationPercentageReport(ReportExecutor executor, AppDbContext db, IDonationPaymentService donations)
    : DonationReportBase(executor, db, donations)
{
    protected override ReportDefinition<DonationReportRow> Definition { get; } = new()
    {
        Key = "donation-percentage",
        Title = "Donation Percentage",
        Supported = ReportFilters.Project,
        Columns =
        [
            new ReportColumn("project", "Project"),
            new ReportColumn("percentage", "Percentage", Numeric: true),
            new ReportColumn("contractValue", "Contract Value", Numeric: true, Total: "contractValue"),
            new ReportColumn("allocated", "Donation Amount", Numeric: true, Total: "allocated"),
        ],
        DefaultSortBy = "project",
        SortKeys = DonationSortKeys.ByLabel,
        Aggregates =
        [
            new ReportAggregate<DonationReportRow>("contractValue", x => x.ContractValue),
            new ReportAggregate<DonationReportRow>("allocated", x => x.Allocated),
        ],
    };

    protected override async ValueTask<IQueryable<DonationReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var data = await LoadAsync(db, ct);
        return data.GroupBy(x => (x.ProjectId, x.Project, x.ContractValue, x.Percentage)).Select(g => new DonationReportRow
        {
            Id = g.Key.ProjectId,
            RowProjectId = g.Key.ProjectId,
            Project = g.Key.Project,
            Percentage = g.Key.Percentage,
            ContractValue = g.Key.ContractValue,
            Allocated = g.Sum(x => x.Allocated),
            RowSearchText = g.Key.Project,
        }).AsQueryable();
    }
}

public sealed class DonationPaidReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<DonationReportRow>(executor, db)
{
    protected override ReportDefinition<DonationReportRow> Definition { get; } = new()
    {
        Key = "donation-paid",
        Title = "Donation Paid",
        Supported = ReportFilters.Date | ReportFilters.Project | ReportFilters.PaymentMode | ReportFilters.Account,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("project", "Project"),
            new ReportColumn("temple", "Temple"),
            new ReportColumn("reference", "Reference"),
            new ReportColumn("paid", "Amount", Numeric: true, Total: "paid"),
        ],
        DefaultSortBy = "date",
        DefaultSortDescending = true,
        SortKeys = new Dictionary<string, Func<IQueryable<DonationReportRow>, bool, IOrderedQueryable<DonationReportRow>>>
        {
            ["date"] = (q, d) => d ? q.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
                                   : q.OrderBy(x => x.Date).ThenBy(x => x.Id),
        },
        Aggregates = [new ReportAggregate<DonationReportRow>("paid", x => x.Paid)],
    };

    protected override ValueTask<IQueryable<DonationReportRow>> SourceAsync(AppDbContext db, CancellationToken ct) =>
        ValueTask.FromResult(
            from s in db.Settlements.AsNoTracking()
            join temple in db.Parties.AsNoTracking() on s.PartyId equals temple.Id
            where s.Direction == SettlementDirection.Out && s.Status == SettlementStatus.Active
                && temple.Types.HasFlag(PartyType.Temple)
            select new DonationReportRow
            {
                Id = s.Id,
                Date = s.Date,
                Project = db.Projects.Where(p => p.Id == s.ProjectId).Select(p => p.Name).FirstOrDefault(),
                Temple = temple.Name,
                Reference = s.ReferenceNo,
                Paid = s.Amount,
                RowDate = s.Date,
                RowProjectId = s.ProjectId,
                RowPartyId = s.PartyId,
                RowPaymentModeId = s.PaymentModeId,
                RowAccountId = s.AccountId,
                RowSearchText = temple.Name,
            });
}

public sealed class DonationDateWiseReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<DonationReportRow>(executor, db)
{
    protected override ReportDefinition<DonationReportRow> Definition { get; } = new()
    {
        Key = "donation-date-wise",
        Title = "Date-wise Donation",
        Supported = ReportFilters.Date | ReportFilters.Project,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("paid", "Paid", Numeric: true, Total: "paid"),
        ],
        DefaultSortBy = "date",
        SortKeys = new Dictionary<string, Func<IQueryable<DonationReportRow>, bool, IOrderedQueryable<DonationReportRow>>>
        {
            ["date"] = (q, d) => d ? q.OrderByDescending(x => x.Date) : q.OrderBy(x => x.Date),
        },
        Aggregates = [new ReportAggregate<DonationReportRow>("paid", x => x.Paid)],
    };

    protected override async ValueTask<IQueryable<DonationReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var payments = await (
            from s in db.Settlements.AsNoTracking()
            join temple in db.Parties.AsNoTracking() on s.PartyId equals temple.Id
            where s.Direction == SettlementDirection.Out && s.Status == SettlementStatus.Active
                && temple.Types.HasFlag(PartyType.Temple)
            select new { s.Date, s.Amount, s.ProjectId }).ToListAsync(ct);

        long serial = 0;
        return payments.GroupBy(p => p.Date).OrderBy(g => g.Key).Select(g => new DonationReportRow
        {
            Id = ++serial,
            Date = g.Key,
            RowDate = g.Key,
            Paid = g.Sum(p => p.Amount),
        }).AsQueryable();
    }
}

internal static class DonationSortKeys
{
    public static readonly IReadOnlyDictionary<string, Func<IQueryable<DonationReportRow>, bool, IOrderedQueryable<DonationReportRow>>> ByLabel =
        new Dictionary<string, Func<IQueryable<DonationReportRow>, bool, IOrderedQueryable<DonationReportRow>>>
        {
            ["project"] = (q, d) => d ? q.OrderByDescending(x => x.Project).ThenBy(x => x.Temple)
                                      : q.OrderBy(x => x.Project).ThenBy(x => x.Temple),
        };
}

internal static class DonationAggregates
{
    public static readonly IReadOnlyList<ReportAggregate<DonationReportRow>> AllocPaidOut =
    [
        new ReportAggregate<DonationReportRow>("allocated", x => x.Allocated),
        new ReportAggregate<DonationReportRow>("paid", x => x.Paid),
        new ReportAggregate<DonationReportRow>("outstanding", x => x.Outstanding),
    ];
}
