using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P6-T01 — company-level Personal / Office / Savings expense entry (BRD §44).</summary>
public sealed class CommonExpenseTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 1700;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"CE P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 5_000_000m, estimatedCost = 4_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> Mode(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/payment-modes"))).EnumerateArray()
        .First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();

    private async Task<long> Account(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true"))).EnumerateArray()
        .First(a => a.GetProperty("name").GetString() == "Office Cash").GetProperty("id").GetInt64();

    private async Task Record(HttpClient c, string type, string sub, decimal amount, long mode, long acct) =>
        (await c.PostAsJsonAsync("/api/v1/common-expenses", new
        {
            type, subCategory = sub, date = "2026-08-05", amount, paymentModeId = mode, accountId = acct,
        })).EnsureSuccessStatusCode();

    [Fact]
    public async Task CommonExpense_Unallocated_AbsentFromProjectReports()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        long project = await Project(c);

        await Record(c, "Personal", "Personal withdrawals", 40_000m, mode, acct);
        await Record(c, "Office", "Office rent", 35_000m, mode, acct);

        (await Json(await c.GetAsync($"/api/v1/projects/{project}/dashboard")))
            .GetProperty("totalExpenses").GetDecimal().Should().Be(0m);
        (await Json(await c.GetAsync($"/api/v1/projects/{project}/budget-vs-actual")))
            .GetProperty("actualCost").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task CommonExpense_PresentInCompanyReports()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);
        long project = await Project(c);

        JsonElement dashBefore = await Json(await c.GetAsync("/api/v1/dashboard"));
        decimal expensesBefore = dashBefore.GetProperty("tiles").EnumerateArray()
            .First(t => t.GetProperty("key").GetString() == "expenses").GetProperty("value").GetDecimal();

        await Record(c, "Personal", "Personal withdrawals", 40_000m, mode, acct);
        await Record(c, "Office", "Office rent", 35_000m, mode, acct);

        JsonElement summary = await Json(await c.GetAsync("/api/v1/common-expenses/summary"));
        summary.GetProperty("personal").GetDecimal().Should().Be(40_000m);
        summary.GetProperty("office").GetDecimal().Should().Be(35_000m);
        summary.GetProperty("total").GetDecimal().Should().Be(75_000m);

        JsonElement dashAfter = await Json(await c.GetAsync("/api/v1/dashboard"));
        decimal expensesAfter = dashAfter.GetProperty("tiles").EnumerateArray()
            .First(t => t.GetProperty("key").GetString() == "expenses").GetProperty("value").GetDecimal();
        (expensesAfter - expensesBefore).Should().Be(75_000m);
    }

    [Fact]
    public async Task Savings_TrackedSeparatelyFromExpense()
    {
        HttpClient c = Admin;
        long mode = await Mode(c);
        long acct = await Account(c);

        await Record(c, "Personal", "Personal purchases", 10_000m, mode, acct);
        await Record(c, "Savings", "Savings allocation", 25_000m, mode, acct);

        JsonElement summary = await Json(await c.GetAsync("/api/v1/common-expenses/summary"));
        summary.GetProperty("savings").GetDecimal().Should().Be(25_000m);
        summary.GetProperty("total").GetDecimal().Should().Be(10_000m); // savings excluded from the expense total

        JsonElement dash = await Json(await c.GetAsync("/api/v1/dashboard"));
        var tiles = dash.GetProperty("tiles").EnumerateArray()
            .ToDictionary(t => t.GetProperty("key").GetString()!, t => t.GetProperty("value").GetDecimal());
        tiles.Should().ContainKey("savings");
        tiles["savings"].Should().Be(25_000m);
    }
}
