using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Identity;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P5-T05 project dashboard, P5-T06 company dashboard (BRD §5, §56).</summary>
public sealed class DashboardReportTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 1600;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c, decimal estimate = 5_000_000m, decimal contract = 6_000_000m) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"Dash P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = contract, estimatedCost = estimate,
        }))).GetProperty("id").GetInt64();

    private async Task<Dictionary<string, long>> Cats(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/expense-categories"))).EnumerateArray()
        .ToDictionary(x => x.GetProperty("slug").GetString()!, x => x.GetProperty("id").GetInt64());

    private async Task<long> Bank(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true"))).EnumerateArray()
        .First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();

    private async Task<long> Cash(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/payment-modes"))).EnumerateArray()
        .First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();

    private async Task Expense(HttpClient c, long project, long cat, decimal amount) =>
        (await c.PostAsJsonAsync("/api/v1/project-expenses", new
        {
            projectId = project, categoryId = cat, date = "2026-08-03", amount, paidImmediately = false,
        })).EnsureSuccessStatusCode();

    private async Task Receipt(HttpClient c, long project, decimal amount)
    {
        long mode = await Cash(c);
        long acct = await Bank(c);
        (await c.PostAsJsonAsync("/api/v1/receipts", new
        {
            projectId = project, type = "ClientAdvance", date = "2026-05-05", amount,
            paymentModeId = mode, accountId = acct,
        })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Dashboard_SingleAggregateEndpoint_And_ExpenseBreakdown_SumsToTotalExpenses()
    {
        HttpClient c = Admin;
        var cat = await Cats(c);
        long project = await Project(c);
        await Expense(c, project, cat["materials"], 1_200_000m);
        await Expense(c, project, cat["electrical"], 300_000m);
        await Receipt(c, project, 900_000m);

        JsonElement d = await Json(await c.GetAsync($"/api/v1/projects/{project}/dashboard"));

        d.GetProperty("summary").GetArrayLength().Should().Be(12);
        d.GetProperty("expenseBreakdown").GetArrayLength().Should().Be(13);

        decimal breakdownSum = d.GetProperty("expenseBreakdown").EnumerateArray()
            .Sum(x => x.GetProperty("amount").GetDecimal());
        breakdownSum.Should().Be(d.GetProperty("totalExpenses").GetDecimal());
        breakdownSum.Should().Be(1_500_000m);
    }

    [Fact]
    public async Task Dashboard_EveryTile_MatchesItsDrilldownTotal()
    {
        HttpClient c = Admin;
        var cat = await Cats(c);
        long project = await Project(c);
        await Expense(c, project, cat["materials"], 2_000_000m);
        await Receipt(c, project, 1_500_000m);

        JsonElement d = await Json(await c.GetAsync($"/api/v1/projects/{project}/dashboard"));
        var tiles = d.GetProperty("summary").EnumerateArray()
            .ToDictionary(x => x.GetProperty("key").GetString()!, x => x.GetProperty("value").GetDecimal());

        // Total Income ties to the income-total endpoint.
        decimal incomeTotal = (await Json(await c.GetAsync($"/api/v1/projects/{project}/income-total")))
            .GetProperty("total").GetDecimal();
        tiles["totalIncome"].Should().Be(incomeTotal).And.Be(1_500_000m);

        // Total Outstanding ties to the project outstanding summary.
        decimal outstanding = (await Json(await c.GetAsync($"/api/v1/projects/{project}/outstanding-summary")))
            .GetProperty("totalPayable").GetDecimal();
        tiles["totalOutstanding"].Should().Be(outstanding);

        // Actual Cost and Total Expenses tie to the financial ledger's cost side.
        tiles["actualCost"].Should().Be(2_000_000m);
        tiles["totalExpenses"].Should().Be(tiles["actualCost"]);
    }

    [Fact]
    public async Task Dashboard_ProjectScopedUser_CannotLoadUnassignedProject()
    {
        HttpClient admin = Admin;
        long allowed = await Project(admin);
        long forbidden = await Project(admin);

        long userId;
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userId = await db.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstAsync();
            db.UserProjectAccess.Add(new UserProjectAccess { UserId = userId, ProjectId = allowed });
            await db.SaveChangesAsync();
        }

        HttpClient scoped = Factory.CreateClientAs(userId: userId, permissions: "dashboard.view");
        (await scoped.GetAsync($"/api/v1/projects/{allowed}/dashboard")).EnsureSuccessStatusCode();
        (await scoped.GetAsync($"/api/v1/projects/{forbidden}/dashboard")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CompanyDashboard_CashPosition_EqualsSumOfAccountBalances()
    {
        HttpClient c = Admin;
        long project = await Project(c);
        await Receipt(c, project, 700_000m); // moves cash into HDFC

        JsonElement accounts = await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true"));
        decimal sum = 0m;
        foreach (JsonElement a in accounts.EnumerateArray())
        {
            sum += (await Json(await c.GetAsync($"/api/v1/accounts/{a.GetProperty("id").GetInt64()}")))
                .GetProperty("balance").GetDecimal();
        }

        JsonElement d = await Json(await c.GetAsync("/api/v1/dashboard"));
        decimal cashTile = d.GetProperty("tiles").EnumerateArray()
            .First(x => x.GetProperty("key").GetString() == "cashBankPosition").GetProperty("value").GetDecimal();
        cashTile.Should().Be(sum);
    }

    [Fact]
    public async Task CompanyDashboard_Totals_EqualSumOfProjectTotals()
    {
        HttpClient c = Admin;
        var cat = await Cats(c);
        long p1 = await Project(c);
        long p2 = await Project(c);
        await Expense(c, p1, cat["materials"], 400_000m);
        await Expense(c, p2, cat["materials"], 600_000m);
        await Receipt(c, p1, 300_000m);

        JsonElement d1 = await Json(await c.GetAsync($"/api/v1/projects/{p1}/dashboard"));
        JsonElement d2 = await Json(await c.GetAsync($"/api/v1/projects/{p2}/dashboard"));
        decimal Tile(JsonElement d, string key) => d.GetProperty("summary").EnumerateArray()
            .First(x => x.GetProperty("key").GetString() == key).GetProperty("value").GetDecimal();

        JsonElement company = await Json(await c.GetAsync("/api/v1/dashboard"));
        var rows = company.GetProperty("projectProfitability").EnumerateArray()
            .Where(x => x.GetProperty("projectId").GetInt64() == p1 || x.GetProperty("projectId").GetInt64() == p2)
            .ToList();

        rows.Sum(x => x.GetProperty("actualCost").GetDecimal())
            .Should().Be(Tile(d1, "actualCost") + Tile(d2, "actualCost"))
            .And.Be(1_000_000m);
        rows.Sum(x => x.GetProperty("revenue").GetDecimal())
            .Should().Be(Tile(d1, "totalIncome") + Tile(d2, "totalIncome"));
    }

    [Fact]
    public async Task CompanyDashboard_And_CompanyPnl_AgreeOnTotalExpenses_IncludingUnallocatedCustom()
    {
        // Product validation (2026-09-04): before this fix, the dashboard's "Overall
        // Expenses" tile and /pnl's company ActualCost disagreed whenever a Personal/
        // Office common expense hadn't yet been through an allocation run, and Custom/
        // Savings didn't count as an expense in either figure. They must now agree, and
        // an un-allocated Custom expense must be counted in both.
        HttpClient c = Admin;
        long project = await Project(c);
        var cat = await Cats(c);
        await Expense(c, project, cat["materials"], 500_000m);
        long mode = await Cash(c);

        (await c.PostAsJsonAsync("/api/v1/common-expenses", new
        {
            type = "Custom", subCategory = "Misc", date = "2026-08-05", amount = 25_000m, paymentModeId = mode,
        })).EnsureSuccessStatusCode();

        JsonElement dash = await Json(await c.GetAsync("/api/v1/dashboard"));
        decimal dashExpenses = dash.GetProperty("tiles").EnumerateArray()
            .First(x => x.GetProperty("key").GetString() == "expenses").GetProperty("value").GetDecimal();

        JsonElement pnl = await Json(await c.GetAsync("/api/v1/pnl"));
        decimal pnlActual = pnl.GetProperty("actualCost").GetDecimal();
        decimal pnlUnallocated = pnl.GetProperty("unallocatedExpense").GetDecimal();

        dashExpenses.Should().Be(pnlActual);
        pnlUnallocated.Should().BeGreaterThanOrEqualTo(25_000m);
    }
}
