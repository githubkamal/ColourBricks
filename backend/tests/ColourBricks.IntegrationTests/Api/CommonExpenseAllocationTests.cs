using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P6-T02..T05 — common expense allocation engine, run, history, report (BRD §45–§47, §56–§57).</summary>
public sealed class CommonExpenseAllocationTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 1800;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> ProjectRow(HttpClient c, string status = "Ongoing") =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"Alloc P {++_seq}", code = $"CB-2026-{_seq}", status,
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 5_000_000m, estimatedCost = 4_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> Mode(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/payment-modes"))).EnumerateArray()
        .First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();

    private async Task<long> Account(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true"))).EnumerateArray()
        .First(a => a.GetProperty("name").GetString() == "Office Cash").GetProperty("id").GetInt64();

    private async Task Expense(HttpClient c, string type, decimal amount, long mode, long acct, string date = "2026-08-10") =>
        (await c.PostAsJsonAsync("/api/v1/common-expenses", new
        {
            type, subCategory = type == "Personal" ? "Personal purchases" : type == "Office" ? "Office rent" : "Savings allocation",
            date, amount, paymentModeId = mode, accountId = acct,
        })).EnsureSuccessStatusCode();

    private static HttpContent Body(string method, string[] types, object[]? shares = null) =>
        JsonContent.Create(new
        {
            periodFrom = "2026-08-01",
            periodTo = "2026-08-31",
            types,
            method,
            shares,
        });

    private async Task<decimal> ProjectActualCost(HttpClient c, long project) =>
        (await Json(await c.GetAsync($"/api/v1/projects/{project}/budget-vs-actual")))
        .GetProperty("actualCost").GetDecimal();

    private static IEnumerable<decimal> Allocated(JsonElement preview) =>
        preview.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("allocated").GetDecimal());

    // ── P6-T02 — allocation engine ───────────────────────────────────────────

    [Fact]
    public async Task Allocation_Equal_BrdSection45FourProjects()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        for (int i = 0; i < 4; i++) await ProjectRow(c);
        await Expense(c, "Personal", 40_000m, mode, acct);
        await Expense(c, "Office", 35_000m, mode, acct);
        await Expense(c, "Savings", 25_000m, mode, acct);

        JsonElement p = await Json(await c.PostAsync("/api/v1/common-expense-allocations/preview",
            Body("Equal", ["Personal", "Office", "Savings"])));

        p.GetProperty("poolAmount").GetDecimal().Should().Be(100_000m);
        Allocated(p).Should().OnlyContain(a => a == 25_000m);
    }

    [Fact]
    public async Task Allocation_Equal_BrdSection45FiveProjects()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        for (int i = 0; i < 5; i++) await ProjectRow(c);
        await Expense(c, "Personal", 40_000m, mode, acct);
        await Expense(c, "Office", 35_000m, mode, acct);
        await Expense(c, "Savings", 25_000m, mode, acct);

        JsonElement p = await Json(await c.PostAsync("/api/v1/common-expense-allocations/preview",
            Body("Equal", ["Personal", "Office", "Savings"])));

        p.GetProperty("poolAmount").GetDecimal().Should().Be(100_000m);
        Allocated(p).Should().OnlyContain(a => a == 20_000m);
    }

    [Fact]
    public async Task Allocation_Equal_IndivisibleAmount_SumsExactly()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        for (int i = 0; i < 3; i++) await ProjectRow(c);
        await Expense(c, "Personal", 100_000m, mode, acct);

        JsonElement preview = await Json(await c.PostAsync("/api/v1/common-expense-allocations/preview",
            Body("Equal", ["Personal"])));

        Allocated(preview).Sum().Should().Be(100_000m);
        preview.GetProperty("balances").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Allocation_Percentage_NotSummingTo100_Returns400()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        long p1 = await ProjectRow(c);
        long p2 = await ProjectRow(c);
        await Expense(c, "Office", 50_000m, mode, acct);

        HttpResponseMessage r = await c.PostAsync("/api/v1/common-expense-allocations/preview",
            Body("Percentage", ["Office"], [new { projectId = p1, value = 60m }, new { projectId = p2, value = 30m }]));

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Allocation_ExcludesNonOngoingProjects()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        long ongoing = await ProjectRow(c);
        long completed = await ProjectRow(c, "Completed");
        await Expense(c, "Personal", 30_000m, mode, acct);

        JsonElement preview = await Json(await c.PostAsync("/api/v1/common-expense-allocations/preview",
            Body("Equal", ["Personal"])));

        var ids = preview.GetProperty("lines").EnumerateArray()
            .Select(l => l.GetProperty("projectId").GetInt64()).ToList();
        ids.Should().Contain(ongoing).And.NotContain(completed);
        preview.GetProperty("lines").EnumerateArray().First()
            .GetProperty("allocated").GetDecimal().Should().Be(30_000m);
    }

    [Fact]
    public async Task Allocation_Manual_MustSumToTotal()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        long p1 = await ProjectRow(c);
        long p2 = await ProjectRow(c);
        await Expense(c, "Office", 100_000m, mode, acct);

        (await c.PostAsync("/api/v1/common-expense-allocations/preview",
            Body("Manual", ["Office"], [new { projectId = p1, value = 60_000m }, new { projectId = p2, value = 30_000m }])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await c.PostAsync("/api/v1/common-expense-allocations/preview",
            Body("Manual", ["Office"], [new { projectId = p1, value = 70_000m }, new { projectId = p2, value = 30_000m }])))
            .EnsureSuccessStatusCode();
    }

    // ── P6-T03 — allocation run screen ───────────────────────────────────────

    [Fact]
    public async Task AllocationPreview_CreatesNoLedgerEntries()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        long p1 = await ProjectRow(c);
        await ProjectRow(c);
        await Expense(c, "Personal", 100_000m, mode, acct);

        decimal costBefore = await ProjectActualCost(c, p1);

        // Preview twice, "navigate away", preview again — a pure read each time.
        (await c.PostAsync("/api/v1/common-expense-allocations/preview", Body("Equal", ["Personal"]))).EnsureSuccessStatusCode();
        (await c.PostAsync("/api/v1/common-expense-allocations/preview", Body("Equal", ["Personal"]))).EnsureSuccessStatusCode();

        (await ProjectActualCost(c, p1)).Should().Be(costBefore);
    }

    [Fact]
    public async Task AllocationCommit_MarksSourceExpensesAllocated()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        long p1 = await ProjectRow(c);
        await ProjectRow(c);
        await Expense(c, "Personal", 100_000m, mode, acct);

        decimal costBefore = await ProjectActualCost(c, p1);
        JsonElement run = await Json(await c.PostAsync("/api/v1/common-expense-allocations", Body("Equal", ["Personal"])));
        long runId = run.GetProperty("id").GetInt64();

        run.GetProperty("poolAmount").GetDecimal().Should().Be(100_000m);
        (await ProjectActualCost(c, p1)).Should().Be(costBefore + 50_000m);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Set<Domain.CommonExpenses.CommonExpense>().CountAsync(e => e.AllocationRunId == runId))
            .Should().Be(1);
    }

    [Fact]
    public async Task DoubleAllocation_SamePeriod_Returns409()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        await ProjectRow(c);
        await Expense(c, "Personal", 100_000m, mode, acct);

        (await c.PostAsync("/api/v1/common-expense-allocations", Body("Equal", ["Personal"]))).EnsureSuccessStatusCode();

        (await c.PostAsync("/api/v1/common-expense-allocations", Body("Equal", ["Personal"])))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── P6-T04 — history and reversal ───────────────────────────────────────

    [Fact]
    public async Task AllocationHistory_RecordsAllBrdSection47Fields()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        long p1 = await ProjectRow(c);
        await ProjectRow(c);
        await Expense(c, "Personal", 40_000m, mode, acct);
        await Expense(c, "Office", 60_000m, mode, acct);

        long runId = (await Json(await c.PostAsync("/api/v1/common-expense-allocations",
            Body("Equal", ["Personal", "Office"])))).GetProperty("id").GetInt64();

        JsonElement history = await Json(await c.GetAsync("/api/v1/common-expense-allocations"));
        JsonElement row = history.EnumerateArray().First(x => x.GetProperty("id").GetInt64() == runId);

        row.GetProperty("types").GetString().Should().Contain("Personal").And.Contain("Office");
        row.GetProperty("method").GetString().Should().Be("Equal");
        row.GetProperty("poolAmount").GetDecimal().Should().Be(100_000m);
        row.GetProperty("periodFrom").GetString().Should().Be("2026-08-01");
        row.GetProperty("periodTo").GetString().Should().Be("2026-08-31");
        row.GetProperty("status").GetString().Should().Be("Active");
        row.GetProperty("createdAtUtc").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UnixEpoch);
        row.TryGetProperty("createdByUserId", out _).Should().BeTrue();
        JsonElement line = row.GetProperty("lines").EnumerateArray().First(l => l.GetProperty("projectId").GetInt64() == p1);
        line.GetProperty("projectName").GetString().Should().NotBeNullOrEmpty();
        line.GetProperty("allocated").GetDecimal().Should().Be(50_000m);
    }

    [Fact]
    public async Task AllocationReversal_RestoresProjectCosts()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        long p1 = await ProjectRow(c);
        long p2 = await ProjectRow(c);
        await Expense(c, "Personal", 40_000m, mode, acct);
        await Expense(c, "Office", 60_000m, mode, acct);

        decimal p1Before = await ProjectActualCost(c, p1);
        decimal p2Before = await ProjectActualCost(c, p2);

        long runId = (await Json(await c.PostAsync("/api/v1/common-expense-allocations",
            Body("Equal", ["Personal", "Office"])))).GetProperty("id").GetInt64();

        (await ProjectActualCost(c, p1)).Should().Be(p1Before + 50_000m);

        (await c.PostAsync($"/api/v1/common-expense-allocations/{runId}/reverse", null)).EnsureSuccessStatusCode();

        (await ProjectActualCost(c, p1)).Should().Be(p1Before);
        (await ProjectActualCost(c, p2)).Should().Be(p2Before);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Set<Domain.CommonExpenses.CommonExpense>().CountAsync(e => e.AllocationRunId == runId))
            .Should().Be(0);
    }

    [Fact]
    public async Task AllocationDrilldown_WorksBothDirections()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        long p1 = await ProjectRow(c);
        await ProjectRow(c);
        await Expense(c, "Personal", 100_000m, mode, acct);

        long runId = (await Json(await c.PostAsync("/api/v1/common-expense-allocations",
            Body("Equal", ["Personal"])))).GetProperty("id").GetInt64();

        // expense/run → projects
        JsonElement run = await Json(await c.GetAsync($"/api/v1/common-expense-allocations/{runId}"));
        run.GetProperty("lines").EnumerateArray()
            .Select(l => l.GetProperty("projectId").GetInt64()).Should().Contain(p1);

        // project ledger line → originating run
        JsonElement ledger = await Json(await c.GetAsync($"/api/v1/projects/{p1}/financial-ledger"));
        JsonElement allocLine = ledger.GetProperty("lines").EnumerateArray()
            .First(l => l.GetProperty("sourceType").GetString() == "CommonExpenseAllocation");
        allocLine.GetProperty("sourceId").GetInt64().Should().Be(runId);
    }

    // ── P6-T05 — common expense reports ─────────────────────────────────────

    [Fact]
    public async Task CommonExpenseReport_TotalsMatchLedger()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        long p1 = await ProjectRow(c);
        long p2 = await ProjectRow(c);
        await Expense(c, "Personal", 30_000m, mode, acct);
        await Expense(c, "Office", 70_000m, mode, acct);

        decimal p1Before = await ProjectActualCost(c, p1);
        decimal p2Before = await ProjectActualCost(c, p2);

        long runId = (await Json(await c.PostAsync("/api/v1/common-expense-allocations",
            Body("Equal", ["Personal", "Office"])))).GetProperty("id").GetInt64();

        JsonElement report = await Json(await c.GetAsync("/api/v1/reports/common-expense-allocations"));
        var runRows = report.EnumerateArray().Where(x => x.GetProperty("runId").GetInt64() == runId).ToList();

        runRows.Sum(x => x.GetProperty("allocated").GetDecimal()).Should().Be(100_000m);
        runRows.Should().OnlyContain(x => x.GetProperty("poolAmount").GetDecimal() == 100_000m);

        // report per-project figure reconciles to the project's actual-cost movement
        decimal p1Row = runRows.First(x => x.GetProperty("projectId").GetInt64() == p1).GetProperty("allocated").GetDecimal();
        p1Row.Should().Be(await ProjectActualCost(c, p1) - p1Before);
        decimal p2Row = runRows.First(x => x.GetProperty("projectId").GetInt64() == p2).GetProperty("allocated").GetDecimal();
        p2Row.Should().Be(await ProjectActualCost(c, p2) - p2Before);
    }

    [Fact]
    public async Task CommonExpenseReport_FiltersByTypeAndPeriod()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        await ProjectRow(c);
        await Expense(c, "Personal", 30_000m, mode, acct);
        await Expense(c, "Office", 70_000m, mode, acct);

        long runId = (await Json(await c.PostAsync("/api/v1/common-expense-allocations",
            Body("Equal", ["Personal", "Office"])))).GetProperty("id").GetInt64();

        // matches inside the period, filtered to a type the run pooled
        JsonElement inPeriod = await Json(await c.GetAsync(
            "/api/v1/reports/common-expense-allocations?dateFrom=2026-08-01&dateTo=2026-08-31&type=Personal"));
        inPeriod.EnumerateArray().Where(x => x.GetProperty("runId").GetInt64() == runId).Should().NotBeEmpty();

        // outside the period → excluded
        JsonElement outOfPeriod = await Json(await c.GetAsync(
            "/api/v1/reports/common-expense-allocations?dateFrom=2026-09-01&dateTo=2026-09-30"));
        outOfPeriod.EnumerateArray().Where(x => x.GetProperty("runId").GetInt64() == runId).Should().BeEmpty();
    }
}
