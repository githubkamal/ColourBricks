using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P5-T02 budget vs actual, P5-T03 project ledger, P5-T04 P&amp;L.</summary>
public sealed class ProjectReportingTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 1500;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c, decimal estimate, decimal contract) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"Report P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = contract, estimatedCost = estimate,
        }))).GetProperty("id").GetInt64();

    private async Task<Dictionary<string, long>> Cats(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/expense-categories"))).EnumerateArray()
        .ToDictionary(x => x.GetProperty("slug").GetString()!, x => x.GetProperty("id").GetInt64());

    private async Task<long> Account(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true"))).EnumerateArray()
        .First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();

    private async Task<long> Mode(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/payment-modes"))).EnumerateArray()
        .First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();

    private async Task Expense(HttpClient c, long project, long categoryId, decimal amount, string date = "2026-08-03") =>
        (await c.PostAsJsonAsync("/api/v1/project-expenses", new
        {
            projectId = project, categoryId, date, amount, paidImmediately = false,
        })).EnsureSuccessStatusCode();

    private async Task Budget(HttpClient c, long project, decimal threshold, params (long cat, decimal amt)[] lines) =>
        (await c.PostAsJsonAsync($"/api/v1/projects/{project}/budget", new
        {
            approachingThresholdPercent = threshold,
            lines = lines.Select(l => new { categoryId = l.cat, amount = l.amt }),
        })).EnsureSuccessStatusCode();

    // ── P5-T02 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task BudgetVsActual_BrdSection40Example()
    {
        HttpClient c = Admin;
        var cat = await Cats(c);
        long project = await Project(c, estimate: 8_000_000m, contract: 10_000_000m);
        await Expense(c, project, cat["materials"], 8_200_000m);

        JsonElement r = await Json(await c.GetAsync($"/api/v1/projects/{project}/budget-vs-actual"));
        r.GetProperty("estimatedCost").GetDecimal().Should().Be(8_000_000m);
        r.GetProperty("actualCost").GetDecimal().Should().Be(8_200_000m);
        r.GetProperty("overrunMessage").GetString().Should().StartWith("Budget Exceeded by");
    }

    [Fact]
    public async Task BudgetVsActual_BrdSection41Table()
    {
        HttpClient c = Admin;
        var cat = await Cats(c);
        long project = await Project(c, 8_000_000m, 10_000_000m);
        await Budget(c, project, 90m,
            (cat["labour"], 2_000_000m), (cat["materials"], 3_500_000m), (cat["electrical"], 500_000m));
        await Expense(c, project, cat["labour"], 1_900_000m);
        await Expense(c, project, cat["materials"], 3_800_000m);
        await Expense(c, project, cat["electrical"], 600_000m);

        JsonElement rows = (await Json(await c.GetAsync($"/api/v1/projects/{project}/budget-vs-actual")))
            .GetProperty("rows");
        var byCat = rows.EnumerateArray().ToDictionary(x => x.GetProperty("categoryId").GetInt64(), x => x);

        // BRD §41: only Materials and Electrical carry the "Over" marker.
        byCat[cat["labour"]].GetProperty("status").GetString().Should().NotBe("Exceeded");
        byCat[cat["materials"]].GetProperty("status").GetString().Should().Be("Exceeded");
        byCat[cat["electrical"]].GetProperty("status").GetString().Should().Be("Exceeded");
        byCat[cat["materials"]].GetProperty("variance").GetDecimal().Should().Be(-300_000m);
    }

    [Fact]
    public async Task ActualCost_UnchangedByPayments()
    {
        HttpClient c = Admin;
        long project = await Project(c, 5_000_000m, 6_000_000m);
        long vendor = (await Json(await c.PostAsJsonAsync(
            "/api/v1/parties?confirm=true", new { name = "AC Vendor", types = new[] { "Vendor" } })))
            .GetProperty("id").GetInt64();
        long mode = await Mode(c);
        long acct = await Account(c);

        (await c.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId = project, vendorId = vendor, date = "2026-05-01", total = 200_000m,
            lines = new[] { new { itemName = "X", quantity = 1m, unit = "Nos", rate = 200_000m, taxAmount = 0m } },
        })).EnsureSuccessStatusCode();

        decimal before = (await Json(await c.GetAsync($"/api/v1/projects/{project}/budget-vs-actual")))
            .GetProperty("actualCost").GetDecimal();
        before.Should().Be(200_000m);

        (await c.PostAsJsonAsync("/api/v1/vendor-payments", new
        {
            vendorId = vendor, projectId = project, date = "2026-05-10", amount = 100_000m,
            paymentModeId = mode, accountId = acct,
        })).EnsureSuccessStatusCode();

        (await Json(await c.GetAsync($"/api/v1/projects/{project}/budget-vs-actual")))
            .GetProperty("actualCost").GetDecimal().Should().Be(before);
    }

    [Fact]
    public async Task Status_TransitionsAtConfiguredThreshold()
    {
        HttpClient c = Admin;
        var cat = await Cats(c);
        long project = await Project(c, 5_000_000m, 6_000_000m);
        await Budget(c, project, 75m, (cat["labour"], 1_000_000m));

        async Task<string?> LabourStatus() =>
            (await Json(await c.GetAsync($"/api/v1/projects/{project}/budget-vs-actual")))
            .GetProperty("rows").EnumerateArray()
            .First(x => x.GetProperty("categoryId").GetInt64() == cat["labour"])
            .GetProperty("status").GetString();

        await Expense(c, project, cat["labour"], 760_000m); // 76% > 75% threshold
        (await LabourStatus()).Should().Be("Approaching");

        await Expense(c, project, cat["labour"], 300_000m); // now 1.06M > budget
        (await LabourStatus()).Should().Be("Exceeded");
    }

    // ── P5-T03 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ledger_RunningBalance_MatchesBrdSection42Example_AndOrdersDeterministically()
    {
        HttpClient c = Admin;
        var cat = await Cats(c);
        long project = await Project(c, 8_000_000m, 15_000_000m);
        long mode = await Mode(c);
        long acct = await Account(c);

        async Task Receipt(decimal amount, string date) =>
            (await c.PostAsJsonAsync("/api/v1/receipts", new
            {
                projectId = project, type = "ClientAdvance", date, amount, paymentModeId = mode, accountId = acct,
            })).EnsureSuccessStatusCode();

        await Receipt(1_000_000m, "2026-08-01");
        await Expense(c, project, cat["materials"], 100_000m, "2026-08-03");
        await Expense(c, project, cat["labour"], 50_000m, "2026-08-05");
        await Expense(c, project, cat["electrical"], 40_000m, "2026-08-07");
        await Receipt(500_000m, "2026-08-10");

        JsonElement view = await Json(await c.GetAsync($"/api/v1/projects/{project}/financial-ledger"));
        var balances = view.GetProperty("lines").EnumerateArray()
            .Select(l => l.GetProperty("runningBalance").GetDecimal()).ToList();

        balances.Should().Equal(1_000_000m, 900_000m, 850_000m, 810_000m, 1_310_000m);
        view.GetProperty("closingBalance").GetDecimal().Should().Be(1_310_000m);
    }

    [Fact]
    public async Task Ledger_EveryRow_HasResolvableSource_AndReversalsMarked()
    {
        HttpClient c = Admin;
        long project = await Project(c, 5_000_000m, 6_000_000m);
        long vendor = (await Json(await c.PostAsJsonAsync(
            "/api/v1/parties?confirm=true", new { name = "Rev Vendor", types = new[] { "Vendor" } })))
            .GetProperty("id").GetInt64();

        long purchaseId = (await Json(await c.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId = project, vendorId = vendor, date = "2026-08-02", total = 300_000m,
            lines = new[] { new { itemName = "X", quantity = 1m, unit = "Nos", rate = 300_000m, taxAmount = 0m } },
        }))).GetProperty("purchase").GetProperty("id").GetInt64();

        (await c.PostAsJsonAsync($"/api/v1/vendor-purchases/{purchaseId}/reverse", new { reason = "wrong project" }))
            .EnsureSuccessStatusCode();

        JsonElement view = await Json(await c.GetAsync($"/api/v1/projects/{project}/financial-ledger"));
        var lines = view.GetProperty("lines").EnumerateArray().ToList();

        lines.Should().OnlyContain(l => l.GetProperty("sourceId").GetInt64() > 0
            && l.GetProperty("sourceType").GetString()!.Length > 0);
        lines.Should().Contain(l => l.GetProperty("isReversal").GetBoolean());
        lines.First(l => l.GetProperty("isReversal").GetBoolean())
            .GetProperty("description").GetString().Should().Contain("reversal");
        view.GetProperty("closingBalance").GetDecimal().Should().Be(0m); // purchase and its reversal net out
    }

    // ── P5-T04 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pnl_RevenueBasisToggle_ChangesRevenueOnly_AndGuardsZero()
    {
        HttpClient c = Admin;
        var cat = await Cats(c);
        long project = await Project(c, 8_000_000m, 10_000_000m);
        long mode = await Mode(c);
        long acct = await Account(c);
        await Expense(c, project, cat["materials"], 6_000_000m);
        (await c.PostAsJsonAsync("/api/v1/receipts", new
        {
            projectId = project, type = "Stage", date = "2026-05-05", amount = 4_000_000m,
            paymentModeId = mode, accountId = acct,
        })).EnsureSuccessStatusCode();

        JsonElement contract = await Json(await c.GetAsync($"/api/v1/projects/{project}/pnl?revenueBasis=Contract"));
        JsonElement receipts = await Json(await c.GetAsync($"/api/v1/projects/{project}/pnl?revenueBasis=Receipts"));

        contract.GetProperty("revenue").GetDecimal().Should().Be(10_000_000m);
        receipts.GetProperty("revenue").GetDecimal().Should().Be(4_000_000m);
        contract.GetProperty("actualCost").GetDecimal().Should().Be(receipts.GetProperty("actualCost").GetDecimal());
        contract.GetProperty("grossProfit").GetDecimal().Should().Be(4_000_000m);   // 10M − 6M
        receipts.GetProperty("grossProfit").GetDecimal().Should().Be(-2_000_000m);  // 4M − 6M

        // Zero-revenue project: profit % is 0, not an error.
        long empty = await Project(c, 1_000_000m, 0m);
        (await Json(await c.GetAsync($"/api/v1/projects/{empty}/pnl?revenueBasis=Contract")))
            .GetProperty("profitPercent").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task Pnl_CompanyRollup_SumsProjectPnl()
    {
        HttpClient c = Admin;
        var cat = await Cats(c);
        long p1 = await Project(c, 1_000_000m, 3_000_000m);
        long p2 = await Project(c, 1_000_000m, 2_000_000m);
        await Expense(c, p1, cat["materials"], 500_000m);
        await Expense(c, p2, cat["materials"], 800_000m);

        JsonElement company = await Json(await c.GetAsync("/api/v1/pnl?revenueBasis=Contract"));
        var rows = company.GetProperty("projects").EnumerateArray()
            .Where(x => x.GetProperty("projectId").GetInt64() == p1 || x.GetProperty("projectId").GetInt64() == p2)
            .ToList();

        rows.Sum(x => x.GetProperty("grossProfit").GetDecimal()).Should().Be(
            (3_000_000m - 500_000m) + (2_000_000m - 800_000m));
        company.GetProperty("revenue").GetDecimal().Should().BeGreaterThanOrEqualTo(5_000_000m);
    }
}
