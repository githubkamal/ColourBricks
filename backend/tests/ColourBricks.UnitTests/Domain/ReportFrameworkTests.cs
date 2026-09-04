using ColourBricks.Application.Auth;
using ColourBricks.Application.Reporting.Framework;
using FluentAssertions;

namespace ColourBricks.UnitTests.Domain;

/// <summary>P8-T01 — the shared report framework: filters, date presets, scope, totals.</summary>
public class ReportFrameworkTests
{
    private sealed record Row : IReportRow
    {
        public int Serial { get; init; }
        public decimal Amount { get; init; }
        public DateOnly? RowDate { get; init; }
        public long? RowProjectId { get; init; }
        public long? RowPartyId { get; init; }
        public long? RowDepartmentId { get; init; }
        public long? RowItemId { get; init; }
        public long? RowCategoryId { get; init; }
        public long? RowPaymentModeId { get; init; }
        public long? RowAccountId { get; init; }
        public string? RowPaymentStatus { get; init; }
        public string? RowTransactionType { get; init; }
        public string? RowReconciliationStatus { get; init; }
        public string? RowSearchText { get; init; }
    }

    private static ReportDefinition<Row> Def(ReportFilters supported = ReportFilters.All) => new()
    {
        Key = "t",
        Title = "T",
        Supported = supported,
        DefaultSortBy = "serial",
        SortKeys = new Dictionary<string, Func<IQueryable<Row>, bool, IOrderedQueryable<Row>>>
        {
            ["serial"] = (q, d) => d ? q.OrderByDescending(x => x.Serial) : q.OrderBy(x => x.Serial),
        },
        Aggregates = [new ReportAggregate<Row>("amount", x => x.Amount)],
    };

    private static readonly DateOnly Today = new(2026, 9, 15); // a Tuesday

    private static (int Count, decimal Total, List<Row> Page) Run(
        IEnumerable<Row> rows, ReportFilter filter, ProjectScope? scope = null, ReportDefinition<Row>? def = null)
    {
        ReportPlan<Row> plan = ReportPlanner.Plan(
            rows.AsQueryable(), def ?? Def(), filter, scope ?? ProjectScope.All, Today);
        return (plan.Filtered.Count(), plan.Filtered.Sum(x => x.Amount), plan.Page.ToList());
    }

    [Fact]
    public void ReportFramework_AppliesAllBrdSection57Filters()
    {
        Row Base(int s) => new()
        {
            Serial = s, Amount = 1m, RowDate = Today,
            RowProjectId = 1, RowPartyId = 1, RowDepartmentId = 1, RowItemId = 1, RowCategoryId = 1,
            RowPaymentModeId = 1, RowAccountId = 1,
            RowPaymentStatus = "Paid", RowTransactionType = "Receipt", RowReconciliationStatus = "Matched",
            RowSearchText = "alpha widget",
        };

        // One row that matches every filter, plus one that differs on each single dimension.
        var rows = new List<Row>
        {
            Base(0),
            Base(1) with { RowProjectId = 99 },
            Base(2) with { RowPartyId = 99 },
            Base(3) with { RowDepartmentId = 99 },
            Base(4) with { RowItemId = 99 },
            Base(5) with { RowCategoryId = 99 },
            Base(6) with { RowPaymentModeId = 99 },
            Base(7) with { RowAccountId = 99 },
            Base(8) with { RowPaymentStatus = "Pending" },
            Base(9) with { RowTransactionType = "Payment" },
            Base(10) with { RowReconciliationStatus = "Unmatched" },
            Base(11) with { RowSearchText = "beta gadget" },
        };

        var checks = new (ReportFilter Filter, int ExcludedSerial)[]
        {
            (new ReportFilter { ProjectId = 1 }, 1),
            (new ReportFilter { VendorId = 1 }, 2),
            (new ReportFilter { DepartmentId = 1 }, 3),
            (new ReportFilter { ItemId = 1 }, 4),
            (new ReportFilter { CategoryId = 1 }, 5),
            (new ReportFilter { PaymentModeId = 1 }, 6),
            (new ReportFilter { AccountId = 1 }, 7),
            (new ReportFilter { PaymentStatus = "Paid" }, 8),
            (new ReportFilter { TransactionType = "Receipt" }, 9),
            (new ReportFilter { ReconciliationStatus = "Matched" }, 10),
            (new ReportFilter { Search = "widget" }, 11),
        };

        foreach ((ReportFilter filter, int excluded) in checks)
        {
            var page = Run(rows, filter).Page;
            page.Should().Contain(r => r.Serial == 0, "the matching row always survives");
            page.Should().NotContain(r => r.Serial == excluded);
        }
    }

    [Theory]
    [InlineData(ReportDatePreset.Today, "2026-09-15", "2026-09-15")]
    [InlineData(ReportDatePreset.Yesterday, "2026-09-14", "2026-09-14")]
    [InlineData(ReportDatePreset.ThisWeek, "2026-09-14", "2026-09-20")]
    [InlineData(ReportDatePreset.PreviousWeek, "2026-09-07", "2026-09-13")]
    [InlineData(ReportDatePreset.ThisMonth, "2026-09-01", "2026-09-30")]
    [InlineData(ReportDatePreset.PreviousMonth, "2026-08-01", "2026-08-31")]
    [InlineData(ReportDatePreset.CurrentYear, "2026-01-01", "2026-12-31")]
    [InlineData(ReportDatePreset.PreviousYear, "2025-01-01", "2025-12-31")]
    [InlineData(ReportDatePreset.ThisFinancialYear, "2026-04-01", "2027-03-31")]
    [InlineData(ReportDatePreset.PreviousFinancialYear, "2025-04-01", "2026-03-31")]
    public void ReportFramework_DatePresets_ResolveCorrectRanges(ReportDatePreset preset, string from, string to)
    {
        DateRange range = ReportDates.Resolve(preset, Today, null, null);

        range.From.Should().Be(DateOnly.Parse(from));
        range.To.Should().Be(DateOnly.Parse(to));
    }

    [Fact]
    public void ReportFramework_DatePresets_CustomPassesThrough()
    {
        var from = new DateOnly(2026, 2, 3);
        var to = new DateOnly(2026, 2, 9);

        ReportDates.Resolve(ReportDatePreset.Custom, Today, from, to).Should().Be(new DateRange(from, to));
    }

    [Fact]
    public void ReportFramework_ProjectScope_AppliedAutomatically()
    {
        var rows = new List<Row>
        {
            new() { Serial = 1, Amount = 10m, RowProjectId = 1 },
            new() { Serial = 2, Amount = 10m, RowProjectId = 2 },
            new() { Serial = 3, Amount = 10m, RowProjectId = 3 },
            new() { Serial = 4, Amount = 10m, RowProjectId = null },
        };

        // No projectId filter, and a definition that does NOT even declare the Project filter.
        var result = Run(rows, new ReportFilter(), ProjectScope.RestrictedTo([1]), Def(ReportFilters.Date));

        result.Count.Should().Be(1);
        result.Page.Should().OnlyContain(r => r.RowProjectId == 1);
    }

    [Fact]
    public void ReportFramework_Pagination_TotalsReflectFullResultSet_NotPage()
    {
        var rows = Enumerable.Range(1, 25)
            .Select(i => new Row { Serial = i, Amount = 10m, RowDate = Today })
            .ToList();

        var result = Run(rows, new ReportFilter { Page = 1, PageSize = 10 });

        result.Page.Should().HaveCount(10);       // one page
        result.Count.Should().Be(25);             // full filtered set
        result.Total.Should().Be(250m);           // total is over all 25 rows, not the 10 on the page
    }
}
