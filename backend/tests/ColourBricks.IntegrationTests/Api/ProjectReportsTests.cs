using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P8-T02 — the seven BRD §49 project reports, on the P8-T01 framework.</summary>
public sealed class ProjectReportsTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 3700;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"PR P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 9_000_000m, estimatedCost = 7_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<Dictionary<string, long>> Cats(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/expense-categories"))).EnumerateArray()
        .ToDictionary(x => x.GetProperty("slug").GetString()!, x => x.GetProperty("id").GetInt64());

    private async Task<long> Vendor(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/parties?confirm=true",
            new { name = $"PR Vendor {++_seq}", types = new[] { "Vendor" } }))).GetProperty("id").GetInt64();

    private async Task Expense(HttpClient c, long project, long cat, decimal amount) =>
        (await c.PostAsJsonAsync("/api/v1/project-expenses", new
        {
            projectId = project, categoryId = cat, date = "2026-08-03", amount, paidImmediately = false,
        })).EnsureSuccessStatusCode();

    private async Task Purchase(HttpClient c, long project, long vendor, decimal amount) =>
        (await c.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId = project, vendorId = vendor, date = "2026-08-04", total = amount,
            lines = new[] { new { itemName = "Cement", quantity = 10m, unit = "Bag", rate = amount / 10m, taxAmount = 0m } },
        })).EnsureSuccessStatusCode();

    private async Task Receipt(HttpClient c, long project, decimal amount)
    {
        long mode = (await Json(await c.GetAsync("/api/v1/payment-modes"))).EnumerateArray()
            .First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();
        long acct = (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true"))).EnumerateArray()
            .First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();
        (await c.PostAsJsonAsync("/api/v1/receipts", new
        {
            projectId = project, type = "ClientAdvance", date = "2026-05-05", amount,
            paymentModeId = mode, accountId = acct,
        })).EnsureSuccessStatusCode();
    }

    private static decimal Total(JsonElement result, string key) =>
        result.GetProperty("totals").GetProperty(key).GetDecimal();

    private async Task<(Dictionary<string, decimal> Tiles, Dictionary<string, decimal> Breakdown)> Dashboard(
        HttpClient c, long project)
    {
        JsonElement d = await Json(await c.GetAsync($"/api/v1/projects/{project}/dashboard"));
        var tiles = d.GetProperty("summary").EnumerateArray()
            .ToDictionary(x => x.GetProperty("key").GetString()!, x => x.GetProperty("value").GetDecimal());
        var breakdown = d.GetProperty("expenseBreakdown").EnumerateArray()
            .ToDictionary(x => x.GetProperty("bucket").GetString()!, x => x.GetProperty("amount").GetDecimal());
        return (tiles, breakdown);
    }

    [Fact]
    public async Task ProjectReports_TotalsMatchDashboard()
    {
        HttpClient c = Admin;
        long project = await Project(c);
        Dictionary<string, long> cats = await Cats(c);
        long vendor = await Vendor(c);

        await Receipt(c, project, 1_500_000m);
        await Purchase(c, project, vendor, 800_000m);            // materials cost + vendor payable
        await Expense(c, project, cats["labour"], 300_000m);      // labour bucket cost
        await Expense(c, project, cats["electrical"], 120_000m);  // another cost bucket

        (Dictionary<string, decimal> tiles, Dictionary<string, decimal> breakdown) = await Dashboard(c, project);
        string q = $"?projectId={project}&datePreset=ThisFinancialYear&pageSize=200";

        JsonElement expense = await Json(await c.GetAsync($"/api/v1/reports/run/project-expense{q}"));
        (Total(expense, "debit") - Total(expense, "credit")).Should().Be(tiles["actualCost"]);

        JsonElement ledger = await Json(await c.GetAsync($"/api/v1/reports/run/project-ledger{q}"));
        Total(ledger, "costNet").Should().Be(tiles["actualCost"]);
        Total(ledger, "incomeNet").Should().Be(tiles["totalIncome"]);

        JsonElement labour = await Json(await c.GetAsync($"/api/v1/reports/run/project-labour{q}"));
        Total(labour, "amount").Should().Be(300_000m);                 // the labour-bucket cost
        Total(labour, "amount").Should().BeLessThanOrEqualTo(tiles["actualCost"]);

        JsonElement material = await Json(await c.GetAsync($"/api/v1/reports/run/project-material{q}"));
        Total(material, "amount").Should().Be(breakdown["Materials"]);

        JsonElement budget = await Json(await c.GetAsync($"/api/v1/reports/run/project-budget-vs-actual{q}"));
        Total(budget, "actual").Should().Be(tiles["actualCost"]);

        JsonElement summary = await Json(await c.GetAsync($"/api/v1/reports/run/project-financial-summary{q}"));
        summary.GetProperty("rows").EnumerateArray().First().GetProperty("profitLoss").GetDecimal()
            .Should().Be(tiles["actualProfit"]);

        JsonElement outstanding = await Json(await c.GetAsync($"/api/v1/reports/run/project-outstanding{q}"));
        Total(outstanding, "payableOnly").Should().Be(tiles["totalOutstanding"]);
    }

    [Fact]
    public async Task ProjectExpenseReport_FiltersByCategoryDepartmentVendor()
    {
        HttpClient c = Admin;
        long project = await Project(c);
        Dictionary<string, long> cats = await Cats(c);
        long vendorA = await Vendor(c);

        await Expense(c, project, cats["electrical"], 50_000m);
        await Expense(c, project, cats["plumbing"], 30_000m);
        await Purchase(c, project, vendorA, 70_000m);

        // by category
        JsonElement byElectrical = await Json(await c.GetAsync(
            $"/api/v1/reports/run/project-expense?projectId={project}&categoryId={cats["electrical"]}&datePreset=ThisFinancialYear&pageSize=200"));
        (Total(byElectrical, "debit") - Total(byElectrical, "credit")).Should().Be(50_000m);

        // by vendor
        JsonElement byVendor = await Json(await c.GetAsync(
            $"/api/v1/reports/run/project-expense?projectId={project}&vendorId={vendorA}&datePreset=ThisFinancialYear&pageSize=200"));
        (Total(byVendor, "debit") - Total(byVendor, "credit")).Should().Be(70_000m);

        // department filter is honoured (no departmental cost here -> empty, but 200 OK)
        JsonElement byDept = await Json(await c.GetAsync(
            $"/api/v1/reports/run/project-expense?projectId={project}&departmentId=999999&datePreset=ThisFinancialYear"));
        byDept.GetProperty("totalCount").GetInt32().Should().Be(0);
    }
}
