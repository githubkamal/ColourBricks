using ColourBricks.Application.Labour;
using ColourBricks.Application.Outstanding;
using ColourBricks.Application.Reporting.Framework;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Reporting.Framework;

/// <summary>P8-T04 — the eight BRD §52 subcontractor / labour reports.</summary>
public sealed class SubReportRow : IReportRow
{
    public long? Id { get; init; }
    public DateOnly? Date { get; init; }
    public string? Team { get; init; }
    public string? Project { get; init; }
    public string? Department { get; init; }
    public string? Kind { get; init; }
    public string? Reference { get; init; }
    public decimal WorkValue { get; init; }
    public decimal Paid { get; init; }
    public decimal Outstanding { get; init; }
    public decimal RunningOutstanding { get; init; }

    public DateOnly? RowDate { get; init; }
    public long? RowProjectId { get; init; }
    public long? RowPartyId { get; init; }
    public long? RowDepartmentId { get; init; }
    public long? RowItemId => null;
    public long? RowCategoryId => null;
    public long? RowPaymentModeId { get; init; }
    public long? RowAccountId { get; init; }
    public string? RowPaymentStatus => null;
    public string? RowTransactionType => null;
    public string? RowReconciliationStatus => null;
    public string? RowSearchText { get; init; }
}

internal static class SubSortKeys
{
    public static readonly IReadOnlyDictionary<string, Func<IQueryable<SubReportRow>, bool, IOrderedQueryable<SubReportRow>>> ByDate =
        new Dictionary<string, Func<IQueryable<SubReportRow>, bool, IOrderedQueryable<SubReportRow>>>
        {
            ["date"] = (q, d) => d ? q.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
                                   : q.OrderBy(x => x.Date).ThenBy(x => x.Id),
        };

    public static readonly IReadOnlyDictionary<string, Func<IQueryable<SubReportRow>, bool, IOrderedQueryable<SubReportRow>>> ByOutstanding =
        new Dictionary<string, Func<IQueryable<SubReportRow>, bool, IOrderedQueryable<SubReportRow>>>
        {
            ["outstanding"] = (q, d) => d ? q.OrderByDescending(x => x.Outstanding) : q.OrderBy(x => x.Outstanding),
            ["team"] = (q, d) => d ? q.OrderByDescending(x => x.Team) : q.OrderBy(x => x.Team),
        };
}

/// <summary>Base for the reports built by aggregating subcontractor work entries and payments.</summary>
public abstract class SubcontractorReportBase(ReportExecutor executor, AppDbContext db)
    : ReportRunner<SubReportRow>(executor, db)
{
    protected static async Task<(
        List<(long Id, long TeamId, string Team, long ProjectId, string Project, long? DeptId, string Dept, DateOnly Date, decimal Agreed)> Work,
        List<(long TeamId, long? ProjectId, DateOnly Date, decimal Amount, long ModeId, long? AccountId, string Ref)> Pay)>
        LoadAsync(AppDbContext db, CancellationToken ct)
    {
        var work = await (
            from o in db.Obligations.AsNoTracking()
            where o.Type == ObligationType.SubcontractorWork && o.Status == ObligationStatus.Active
            select new
            {
                o.Id,
                TeamId = o.PartyId!.Value,
                Team = db.Parties.Where(p => p.Id == o.PartyId).Select(p => p.Name).FirstOrDefault() ?? "",
                o.ProjectId,
                Project = db.Projects.Where(p => p.Id == o.ProjectId).Select(p => p.Name).FirstOrDefault() ?? "",
                DeptId = o.DepartmentId,
                Dept = db.Departments.Where(d => d.Id == o.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? "",
                o.Date,
                Agreed = o.Amount,
            }).ToListAsync(ct);

        var pay = await (
            from s in db.Settlements.AsNoTracking()
            join party in db.Parties.AsNoTracking() on s.PartyId equals party.Id
            where s.Direction == SettlementDirection.Out && s.Status == SettlementStatus.Active
                && party.Types.HasFlag(PartyType.Subcontractor)
            select new { TeamId = s.PartyId!.Value, s.ProjectId, s.Date, s.Amount, ModeId = s.PaymentModeId, s.AccountId, Ref = s.ReferenceNo ?? "" })
            .ToListAsync(ct);

        return (
            work.Select(w => (w.Id, w.TeamId, w.Team, w.ProjectId, w.Project, w.DeptId, w.Dept, w.Date, w.Agreed)).ToList(),
            pay.Select(p => (p.TeamId, p.ProjectId, p.Date, p.Amount, p.ModeId, p.AccountId, p.Ref)).ToList());
    }
}

public sealed class SubcontractorWorkValueVsPaymentReport(
    ReportExecutor executor, AppDbContext db, IOutstandingService outstanding)
    : SubcontractorReportBase(executor, db)
{
    protected override ReportDefinition<SubReportRow> Definition { get; } = new()
    {
        Key = "subcontractor-work-value-vs-payment",
        Title = "Work Value vs Payment",
        Supported = ReportFilters.Subcontractor | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("team", "Team"),
            new ReportColumn("workValue", "Work Value", Numeric: true, Total: "workValue"),
            new ReportColumn("paid", "Paid", Numeric: true, Total: "paid"),
            new ReportColumn("outstanding", "Outstanding", Numeric: true, Total: "outstanding"),
        ],
        DefaultSortBy = "outstanding",
        DefaultSortDescending = true,
        SortKeys = SubSortKeys.ByOutstanding,
        Aggregates =
        [
            new ReportAggregate<SubReportRow>("workValue", x => x.WorkValue),
            new ReportAggregate<SubReportRow>("paid", x => x.Paid),
            new ReportAggregate<SubReportRow>("outstanding", x => x.Outstanding),
        ],
    };

    protected override async ValueTask<IQueryable<SubReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        (var work, var pay) = await LoadAsync(db, ct);
        var rows = new List<SubReportRow>();
        foreach (var g in work.GroupBy(w => (w.TeamId, w.Team)))
        {
            decimal workValue = g.Sum(w => w.Agreed);
            decimal paid = pay.Where(p => p.TeamId == g.Key.TeamId).Sum(p => p.Amount);
            decimal svcOutstanding = await outstanding.SubcontractorTotalAsync(g.Key.TeamId, ct);
            rows.Add(new SubReportRow
            {
                Id = g.Key.TeamId,
                RowPartyId = g.Key.TeamId,
                Team = g.Key.Team,
                WorkValue = workValue,
                Paid = paid,
                Outstanding = svcOutstanding,
                RowSearchText = g.Key.Team,
            });
        }

        return rows.AsQueryable();
    }
}

public sealed class SubcontractorTeamExpenseReport(ReportExecutor executor, AppDbContext db)
    : SubcontractorReportBase(executor, db)
{
    protected override ReportDefinition<SubReportRow> Definition { get; } = new()
    {
        Key = "subcontractor-team-expense",
        Title = "Team-wise Expense",
        Supported = ReportFilters.Subcontractor | ReportFilters.Date,
        Columns =
        [
            new ReportColumn("team", "Team"),
            new ReportColumn("workValue", "Work Value", Numeric: true, Total: "workValue"),
            new ReportColumn("paid", "Paid", Numeric: true, Total: "paid"),
        ],
        DefaultSortBy = "team",
        SortKeys = SubSortKeys.ByOutstanding,
        Aggregates =
        [
            new ReportAggregate<SubReportRow>("workValue", x => x.WorkValue),
            new ReportAggregate<SubReportRow>("paid", x => x.Paid),
        ],
    };

    protected override async ValueTask<IQueryable<SubReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        (var work, var pay) = await LoadAsync(db, ct);
        return work.GroupBy(w => (w.TeamId, w.Team)).Select(g => new SubReportRow
        {
            Id = g.Key.TeamId,
            RowPartyId = g.Key.TeamId,
            Team = g.Key.Team,
            WorkValue = g.Sum(w => w.Agreed),
            Paid = pay.Where(p => p.TeamId == g.Key.TeamId).Sum(p => p.Amount),
            RowSearchText = g.Key.Team,
        }).AsQueryable();
    }
}

public sealed class SubcontractorDepartmentExpenseReport(ReportExecutor executor, AppDbContext db)
    : SubcontractorReportBase(executor, db)
{
    protected override ReportDefinition<SubReportRow> Definition { get; } = new()
    {
        Key = "subcontractor-department-expense",
        Title = "Department-wise Expense",
        Supported = ReportFilters.Department | ReportFilters.Date,
        Columns =
        [
            new ReportColumn("department", "Department"),
            new ReportColumn("workValue", "Work Value", Numeric: true, Total: "workValue"),
        ],
        DefaultSortBy = "department",
        SortKeys = new Dictionary<string, Func<IQueryable<SubReportRow>, bool, IOrderedQueryable<SubReportRow>>>
        {
            ["department"] = (q, d) => d ? q.OrderByDescending(x => x.Department) : q.OrderBy(x => x.Department),
        },
        Aggregates = [new ReportAggregate<SubReportRow>("workValue", x => x.WorkValue)],
    };

    protected override async ValueTask<IQueryable<SubReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        (var work, _) = await LoadAsync(db, ct);
        return work.GroupBy(w => (w.DeptId, w.Dept)).Select(g => new SubReportRow
        {
            Id = g.Key.DeptId,
            RowDepartmentId = g.Key.DeptId,
            Department = g.Key.Dept,
            WorkValue = g.Sum(w => w.Agreed),
            RowSearchText = g.Key.Dept,
        }).AsQueryable();
    }
}

public sealed class SubcontractorProjectTeamExpenseReport(ReportExecutor executor, AppDbContext db)
    : SubcontractorReportBase(executor, db)
{
    protected override ReportDefinition<SubReportRow> Definition { get; } = new()
    {
        Key = "subcontractor-project-team-expense",
        Title = "Project-wise Team Expense",
        Supported = ReportFilters.Subcontractor | ReportFilters.Project,
        Columns =
        [
            new ReportColumn("project", "Project"),
            new ReportColumn("team", "Team"),
            new ReportColumn("workValue", "Work Value", Numeric: true, Total: "workValue"),
        ],
        DefaultSortBy = "project",
        SortKeys = new Dictionary<string, Func<IQueryable<SubReportRow>, bool, IOrderedQueryable<SubReportRow>>>
        {
            ["project"] = (q, d) => d ? q.OrderByDescending(x => x.Project).ThenBy(x => x.Team)
                                      : q.OrderBy(x => x.Project).ThenBy(x => x.Team),
        },
        Aggregates = [new ReportAggregate<SubReportRow>("workValue", x => x.WorkValue)],
    };

    protected override async ValueTask<IQueryable<SubReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        (var work, _) = await LoadAsync(db, ct);
        return work.GroupBy(w => (w.ProjectId, w.Project, w.TeamId, w.Team)).Select(g => new SubReportRow
        {
            RowProjectId = g.Key.ProjectId,
            RowPartyId = g.Key.TeamId,
            Project = g.Key.Project,
            Team = g.Key.Team,
            WorkValue = g.Sum(w => w.Agreed),
            RowSearchText = g.Key.Project + " " + g.Key.Team,
        }).AsQueryable();
    }
}

public sealed class SubcontractorOutstandingReport(
    ReportExecutor executor, AppDbContext db, IOutstandingService outstanding)
    : SubcontractorReportBase(executor, db)
{
    protected override ReportDefinition<SubReportRow> Definition { get; } = new()
    {
        Key = "subcontractor-outstanding",
        Title = "Subcontractor Outstanding Report",
        Supported = ReportFilters.Subcontractor,
        Columns =
        [
            new ReportColumn("team", "Team"),
            new ReportColumn("workValue", "Total Work", Numeric: true, Total: "workValue"),
            new ReportColumn("paid", "Total Paid", Numeric: true, Total: "paid"),
            new ReportColumn("outstanding", "Outstanding", Numeric: true, Total: "outstanding"),
        ],
        DefaultSortBy = "outstanding",
        DefaultSortDescending = true,
        SortKeys = SubSortKeys.ByOutstanding,
        Aggregates =
        [
            new ReportAggregate<SubReportRow>("workValue", x => x.WorkValue),
            new ReportAggregate<SubReportRow>("paid", x => x.Paid),
            new ReportAggregate<SubReportRow>("outstanding", x => x.Outstanding),
        ],
    };

    protected override async ValueTask<IQueryable<SubReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        (var work, var pay) = await LoadAsync(db, ct);
        var rows = new List<SubReportRow>();
        foreach (var g in work.GroupBy(w => (w.TeamId, w.Team)))
        {
            rows.Add(new SubReportRow
            {
                Id = g.Key.TeamId,
                RowPartyId = g.Key.TeamId,
                Team = g.Key.Team,
                WorkValue = g.Sum(w => w.Agreed),
                Paid = pay.Where(p => p.TeamId == g.Key.TeamId).Sum(p => p.Amount),
                Outstanding = await outstanding.SubcontractorTotalAsync(g.Key.TeamId, ct),
                RowSearchText = g.Key.Team,
            });
        }

        return rows.AsQueryable();
    }
}

public sealed class SubcontractorPaymentHistoryReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<SubReportRow>(executor, db)
{
    protected override ReportDefinition<SubReportRow> Definition { get; } = new()
    {
        Key = "subcontractor-payment-history",
        Title = "Subcontractor Payment History",
        Supported = ReportFilters.Date | ReportFilters.Subcontractor | ReportFilters.Project
            | ReportFilters.PaymentMode | ReportFilters.Account,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("team", "Team"),
            new ReportColumn("project", "Project"),
            new ReportColumn("reference", "Reference"),
            new ReportColumn("paid", "Amount", Numeric: true, Total: "paid"),
        ],
        DefaultSortBy = "date",
        DefaultSortDescending = true,
        SortKeys = SubSortKeys.ByDate,
        Aggregates = [new ReportAggregate<SubReportRow>("paid", x => x.Paid)],
    };

    protected override ValueTask<IQueryable<SubReportRow>> SourceAsync(AppDbContext db, CancellationToken ct) =>
        ValueTask.FromResult(
            from s in db.Settlements.AsNoTracking()
            join party in db.Parties.AsNoTracking() on s.PartyId equals party.Id
            where s.Direction == SettlementDirection.Out && s.Status == SettlementStatus.Active
                && party.Types.HasFlag(PartyType.Subcontractor)
            select new SubReportRow
            {
                Id = s.Id,
                Date = s.Date,
                Team = party.Name,
                Project = db.Projects.Where(p => p.Id == s.ProjectId).Select(p => p.Name).FirstOrDefault(),
                Reference = s.ReferenceNo,
                Paid = s.Amount,
                RowDate = s.Date,
                RowPartyId = s.PartyId,
                RowProjectId = s.ProjectId,
                RowPaymentModeId = s.PaymentModeId,
                RowAccountId = s.AccountId,
                RowSearchText = party.Name,
            });
}

public sealed class SubcontractorDateWisePaymentReport(ReportExecutor executor, AppDbContext db)
    : SubcontractorReportBase(executor, db)
{
    protected override ReportDefinition<SubReportRow> Definition { get; } = new()
    {
        Key = "subcontractor-date-wise-payment",
        Title = "Date-wise Payment Report",
        Supported = ReportFilters.Date | ReportFilters.Subcontractor,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("paid", "Paid", Numeric: true, Total: "paid"),
        ],
        DefaultSortBy = "date",
        SortKeys = SubSortKeys.ByDate,
        Aggregates = [new ReportAggregate<SubReportRow>("paid", x => x.Paid)],
    };

    protected override async ValueTask<IQueryable<SubReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        (_, var pay) = await LoadAsync(db, ct);
        long serial = 0;
        return pay.GroupBy(p => p.Date).OrderBy(g => g.Key).Select(g => new SubReportRow
        {
            Id = ++serial,
            Date = g.Key,
            RowDate = g.Key,
            Paid = g.Sum(p => p.Amount),
        }).AsQueryable();
    }
}

public sealed class SubcontractorStatementReport(
    ReportExecutor executor, AppDbContext db, ILabourService labour)
    : ReportRunner<SubReportRow>(executor, db)
{
    protected override ReportDefinition<SubReportRow> Definition { get; } = new()
    {
        Key = "subcontractor-statement",
        Title = "Subcontractor Statement",
        Supported = ReportFilters.Date | ReportFilters.Subcontractor,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("kind", "Type"),
            new ReportColumn("reference", "Reference"),
            new ReportColumn("workValue", "Work Value", Numeric: true, Total: "workValue"),
            new ReportColumn("paid", "Paid", Numeric: true, Total: "paid"),
            new ReportColumn("runningOutstanding", "Outstanding", Numeric: true),
        ],
        DefaultSortBy = "date",
        SortKeys = SubSortKeys.ByDate,
        Aggregates =
        [
            new ReportAggregate<SubReportRow>("workValue", x => x.WorkValue),
            new ReportAggregate<SubReportRow>("paid", x => x.Paid),
        ],
    };

    protected override async ValueTask<IQueryable<SubReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var teams = await db.Parties.AsNoTracking()
            .Where(p => p.Types.HasFlag(PartyType.Subcontractor))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);

        var rows = new List<SubReportRow>();
        long serial = 0;
        foreach (var team in teams)
        {
            foreach (TeamStatementRowDto r in await labour.TeamStatementAsync(team.Id, ct))
            {
                rows.Add(new SubReportRow
                {
                    Id = ++serial,
                    Date = r.Date,
                    RowDate = r.Date,
                    RowPartyId = team.Id,
                    Team = team.Name,
                    Kind = r.Kind,
                    Reference = r.Reference,
                    WorkValue = r.WorkValue,
                    Paid = r.Paid,
                    RunningOutstanding = r.RunningOutstanding,
                    RowSearchText = team.Name,
                });
            }
        }

        return rows.AsQueryable();
    }
}
