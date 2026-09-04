using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P7-T04 — upcoming / overdue EMI alerts and outstanding summary (BRD §48, §66).</summary>
public sealed class LoanAlertTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 3400;

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Lender(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/parties?confirm=true",
            new { name = $"Alert Lender {++_seq}", types = new[] { "Lender" } }))).GetProperty("id").GetInt64();

    private async Task<long> AccountId(HttpClient c, string name) =>
        (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<long> ModeId(HttpClient c, string name) =>
        (await Json(await c.GetAsync("/api/v1/payment-modes")))
        .EnumerateArray().First(m => m.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<(long LoanId, JsonElement Schedule)> LoanWithSchedule(
        HttpClient c, DateOnly emiStart, int tenure = 12)
    {
        long lenderId = await Lender(c);
        long bank = await AccountId(c, "HDFC");
        long loanId = (await Json(await c.PostAsJsonAsync("/api/v1/loans", new
        {
            lenderId,
            principalAmount = 1_200_000m,
            annualInterestRatePercent = 9m,
            startDate = emiStart.AddMonths(-1).ToString("yyyy-MM-dd"),
            tenureMonths = tenure,
            emiStartDate = emiStart.ToString("yyyy-MM-dd"),
            disbursementAccountId = bank,
            disbursementDate = emiStart.AddMonths(-1).ToString("yyyy-MM-dd"),
        }))).GetProperty("id").GetInt64();

        JsonElement schedule = await Json(await c.PostAsJsonAsync($"/api/v1/loans/{loanId}/schedule", new { }));
        return (loanId, schedule);
    }

    private static long InstalmentId(JsonElement schedule, int no) =>
        schedule.EnumerateArray().First(x => x.GetProperty("instalmentNo").GetInt32() == no).GetProperty("id").GetInt64();

    [Fact]
    public async Task UpcomingEmi_IncludesWithinWindow_ExcludesOutside()
    {
        HttpClient c = Client;
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        (long loanId, JsonElement schedule) = await LoanWithSchedule(c, today.AddDays(2));

        JsonElement alerts = await Json(await c.GetAsync("/api/v1/loans/alerts?daysAhead=7"));

        var upcoming = alerts.GetProperty("upcoming").EnumerateArray()
            .Where(a => a.GetProperty("loanId").GetInt64() == loanId).ToList();
        upcoming.Select(a => a.GetProperty("instalmentId").GetInt64())
            .Should().Contain(InstalmentId(schedule, 1))
            .And.NotContain(InstalmentId(schedule, 2)); // due ~a month out, past the 7-day window
    }

    [Fact]
    public async Task OverdueEmi_ExcludesPaidInstalments()
    {
        HttpClient c = Client;
        long cash = await ModeId(c, "Cash");
        long bank = await AccountId(c, "HDFC");
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        (long loanId, JsonElement schedule) = await LoanWithSchedule(c, today.AddDays(-40));

        long paid = InstalmentId(schedule, 1);
        (await c.PostAsJsonAsync($"/api/v1/loans/{loanId}/emi-payments", new
        {
            instalmentId = paid, date = today.ToString("yyyy-MM-dd"), paymentModeId = cash, accountId = bank,
        })).EnsureSuccessStatusCode();

        JsonElement alerts = await Json(await c.GetAsync("/api/v1/loans/alerts?daysAhead=7"));
        var overdue = alerts.GetProperty("overdue").EnumerateArray()
            .Where(a => a.GetProperty("loanId").GetInt64() == loanId)
            .Select(a => a.GetProperty("instalmentId").GetInt64()).ToList();

        overdue.Should().Contain(InstalmentId(schedule, 2)).And.NotContain(paid);
    }

    [Fact]
    public async Task Alerts_AreIdempotentAcrossRuns()
    {
        HttpClient c = Client;
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        await LoanWithSchedule(c, today.AddDays(-40));

        await c.GetAsync("/api/v1/loans/alerts?daysAhead=7");

        int countAfterFirst;
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            countAfterFirst = await db.Set<Domain.Loans.LoanAlert>().CountAsync();
        }

        JsonElement second = await Json(await c.GetAsync("/api/v1/loans/alerts?daysAhead=7"));

        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Set<Domain.Loans.LoanAlert>().CountAsync()).Should().Be(countAfterFirst);
        }

        second.GetProperty("overdue").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task OutstandingSummary_NetsPrincipalRepaid_AndGroupsByProject()
    {
        HttpClient c = Client;
        long cash = await ModeId(c, "Cash");
        long bank = await AccountId(c, "HDFC");
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        (long loanId, JsonElement schedule) = await LoanWithSchedule(c, today.AddDays(-40));

        decimal firstPrincipal = schedule.EnumerateArray().First().GetProperty("principalComponent").GetDecimal();
        (await c.PostAsJsonAsync($"/api/v1/loans/{loanId}/emi-payments", new
        {
            instalmentId = InstalmentId(schedule, 1), date = today.ToString("yyyy-MM-dd"),
            paymentModeId = cash, accountId = bank,
        })).EnsureSuccessStatusCode();

        JsonElement summary = await Json(await c.GetAsync("/api/v1/loans/outstanding-summary"));
        decimal expectedForLoan = 1_200_000m - firstPrincipal;

        summary.GetProperty("companyPrincipalOutstanding").GetDecimal().Should().BeGreaterThanOrEqualTo(expectedForLoan);
        summary.GetProperty("byProject").EnumerateArray()
            .Sum(r => r.GetProperty("principalOutstanding").GetDecimal())
            .Should().Be(summary.GetProperty("companyPrincipalOutstanding").GetDecimal());
    }
}
