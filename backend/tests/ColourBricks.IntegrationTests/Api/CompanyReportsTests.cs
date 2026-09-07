using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P8-T06 — company-level reports and analytics (BRD §56).</summary>
public sealed class CompanyReportsTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 4100;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c, decimal contract) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"CR P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = contract, estimatedCost = contract * 0.7m,
        }))).GetProperty("id").GetInt64();

    private async Task<(long Mode, long Account)> Cash(HttpClient c) => (
        (await Json(await c.GetAsync("/api/v1/payment-modes"))).EnumerateArray()
            .First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64(),
        (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true"))).EnumerateArray()
            .First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64());

    private async Task Receipt(HttpClient c, long project, decimal amount, long mode, long account, string date) =>
        (await c.PostAsJsonAsync("/api/v1/receipts", new
        {
            projectId = project, type = "ClientAdvance", date, amount, paymentModeId = mode, accountId = account,
        })).EnsureSuccessStatusCode();

    private async Task Expense(HttpClient c, long project, decimal amount, string date)
    {
        long cat = (await Json(await c.GetAsync("/api/v1/expense-categories"))).EnumerateArray()
            .First(x => x.GetProperty("slug").GetString() == "electrical").GetProperty("id").GetInt64();
        (await c.PostAsJsonAsync("/api/v1/project-expenses", new
        {
            projectId = project, categoryId = cat, date, amount, paidImmediately = false,
        })).EnsureSuccessStatusCode();
    }

    private static decimal Total(JsonElement r, string key) => r.GetProperty("totals").GetProperty(key).GetDecimal();
    private static IEnumerable<JsonElement> Rows(JsonElement r) => r.GetProperty("rows").EnumerateArray();

    [Fact]
    public async Task CompanyProfit_EqualsIncomeMinusExpenses()
    {
        HttpClient c = Admin;
        (long mode, long acct) = await Cash(c);
        long p = await Project(c, 5_000_000m);
        await Receipt(c, p, 1_200_000m, mode, acct, "2026-05-10");
        await Expense(c, p, 400_000m, "2026-06-15");

        JsonElement summary = await Json(await c.GetAsync("/api/v1/reports/run/company-summary?pageSize=50"));
        var byKey = Rows(summary).ToDictionary(x => x.GetProperty("metric").GetString()!, x => x.GetProperty("value").GetDecimal());

        (byKey["income"] - byKey["expenses"]).Should().Be(byKey["profitLoss"]);

        // ... and it agrees with the company dashboard.
        JsonElement dash = await Json(await c.GetAsync("/api/v1/dashboard?period=Entire"));
        decimal dashProfit = dash.GetProperty("tiles").EnumerateArray()
            .First(t => t.GetProperty("key").GetString() == "profitLoss").GetProperty("value").GetDecimal();
        byKey["profitLoss"].Should().Be(dashProfit);
    }

    [Fact]
    public async Task CompanyReports_MonthlySum_EqualsAnnual()
    {
        HttpClient c = Admin;
        (long mode, long acct) = await Cash(c);
        long p = await Project(c, 6_000_000m);
        await Receipt(c, p, 500_000m, mode, acct, "2026-05-10");
        await Receipt(c, p, 300_000m, mode, acct, "2026-07-10");
        await Expense(c, p, 200_000m, "2026-06-15");
        await Expense(c, p, 150_000m, "2026-08-15");

        JsonElement monthly = await Json(await c.GetAsync("/api/v1/reports/run/company-monthly?pageSize=500"));

        // Every row: profit is income minus expense.
        foreach (JsonElement row in Rows(monthly))
        {
            (row.GetProperty("income").GetDecimal() - row.GetProperty("expense").GetDecimal())
                .Should().Be(row.GetProperty("profit").GetDecimal());
        }

        // The monthly totals sum consistently: Σincome − Σexpense == Σprofit (the "annual" figure).
        (Total(monthly, "income") - Total(monthly, "expense")).Should().Be(Total(monthly, "profit"));
        Total(monthly, "profit").Should().NotBe(0m);
    }

    [Fact]
    public async Task ProjectProfitabilityRanking_OrdersCorrectly()
    {
        HttpClient c = Admin;
        (long mode, long acct) = await Cash(c);

        long rich = await Project(c, 8_000_000m);
        long poor = await Project(c, 3_000_000m);
        await Receipt(c, rich, 2_000_000m, mode, acct, "2026-05-10");
        await Expense(c, rich, 300_000m, "2026-06-15");    // profit ~ +1,700,000
        await Receipt(c, poor, 100_000m, mode, acct, "2026-05-11");
        await Expense(c, poor, 900_000m, "2026-06-16");    // profit ~ -800,000

        JsonElement ranking = await Json(await c.GetAsync(
            "/api/v1/reports/run/project-profitability-ranking?pageSize=500"));

        var profits = Rows(ranking).Select(r => r.GetProperty("profit").GetDecimal()).ToList();
        profits.Should().BeInDescendingOrder();

        var order = Rows(ranking).Select(r => r.GetProperty("id").GetInt64()).ToList();
        order.IndexOf(rich).Should().BeLessThan(order.IndexOf(poor));
    }
}
