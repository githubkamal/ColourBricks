using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P7-T05 — BRD §54 loan reports reconcile to the ledger.</summary>
public sealed class LoanReportTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 3500;

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"Rep Loan P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2029-03-31",
            contractValue = 80_000_000m, estimatedCost = 60_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> Lender(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/parties?confirm=true",
            new { name = $"Rep Lender {++_seq}", types = new[] { "Lender" } }))).GetProperty("id").GetInt64();

    private async Task<long> AccountId(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();

    private async Task<long> ModeId(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/payment-modes")))
        .EnumerateArray().First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();

    private async Task<(long LoanId, decimal InterestPaid)> LoanPaidThreeInstalments(HttpClient c)
    {
        long projectId = await Project(c);
        long lenderId = await Lender(c);
        long bank = await AccountId(c);
        long cash = await ModeId(c);

        long loanId = (await Json(await c.PostAsJsonAsync("/api/v1/loans", new
        {
            lenderId, projectId,
            principalAmount = 5_000_000m, annualInterestRatePercent = 9m,
            startDate = "2026-06-01", tenureMonths = 60, emiStartDate = "2026-07-01",
            disbursementAccountId = bank, disbursementDate = "2026-06-01",
        }))).GetProperty("id").GetInt64();

        JsonElement schedule = await Json(await c.PostAsJsonAsync($"/api/v1/loans/{loanId}/schedule", new { }));

        decimal interest = 0m;
        foreach (int no in new[] { 1, 2, 3 })
        {
            JsonElement row = schedule.EnumerateArray().First(x => x.GetProperty("instalmentNo").GetInt32() == no);
            interest += row.GetProperty("interestComponent").GetDecimal();
            (await c.PostAsJsonAsync($"/api/v1/loans/{loanId}/emi-payments", new
            {
                instalmentId = row.GetProperty("id").GetInt64(),
                date = "2026-09-01", paymentModeId = cash, accountId = bank,
            })).EnsureSuccessStatusCode();
        }

        return (loanId, interest);
    }

    [Fact]
    public async Task LoanReport_PrincipalPaidPlusOutstanding_EqualsLoanAmount()
    {
        HttpClient c = Client;
        (long loanId, _) = await LoanPaidThreeInstalments(c);

        foreach (string path in new[]
                 {
                     $"/api/v1/reports/loans/principal-vs-interest?loanId={loanId}",
                     $"/api/v1/reports/loans/outstanding",
                 })
        {
            JsonElement report = await Json(await c.GetAsync(path));
            JsonElement row = report.EnumerateArray().First(r => r.GetProperty("loanId").GetInt64() == loanId);

            (row.GetProperty("principalPaid").GetDecimal() + row.GetProperty("outstandingPrincipal").GetDecimal())
                .Should().Be(row.GetProperty("loanAmount").GetDecimal());
        }
    }

    [Fact]
    public async Task LoanReport_InterestPaid_MatchesLedgerInterestExpense()
    {
        HttpClient c = Client;
        (long loanId, decimal scheduledInterest) = await LoanPaidThreeInstalments(c);

        JsonElement report = await Json(await c.GetAsync(
            $"/api/v1/reports/loans/principal-vs-interest?loanId={loanId}"));
        decimal reported = report.EnumerateArray()
            .First(r => r.GetProperty("loanId").GetInt64() == loanId).GetProperty("interestPaid").GetDecimal();

        reported.Should().Be(scheduledInterest);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        decimal fromPayments = await db.Set<Domain.Loans.LoanEmiPayment>()
            .Where(p => p.LoanId == loanId && p.Status == Domain.Loans.LoanEmiPaymentStatus.Active)
            .SumAsync(p => p.InterestPaid);
        reported.Should().Be(fromPayments);
    }

    [Fact]
    public async Task LoanReports_AllSevenRespond()
    {
        HttpClient c = Client;
        (long loanId, _) = await LoanPaidThreeInstalments(c);

        foreach (string path in new[]
                 {
                     "/api/v1/reports/loans/project-wise",
                     "/api/v1/reports/loans/outstanding",
                     $"/api/v1/reports/loans/schedule/{loanId}",
                     $"/api/v1/reports/loans/emi-paid?loanId={loanId}",
                     "/api/v1/reports/loans/emi-pending",
                     "/api/v1/reports/loans/principal-vs-interest",
                     "/api/v1/reports/loans/date-wise?dateFrom=2026-09-01&dateTo=2026-09-30",
                 })
        {
            HttpResponseMessage r = await c.GetAsync(path);
            r.IsSuccessStatusCode.Should().BeTrue($"{path} -> {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        }

        JsonElement dateWise = await Json(await c.GetAsync(
            "/api/v1/reports/loans/date-wise?dateFrom=2026-09-01&dateTo=2026-09-30"));
        dateWise.EnumerateArray().First(r => r.GetProperty("date").GetString() == "2026-09-01")
            .GetProperty("paymentCount").GetInt32().Should().BeGreaterThanOrEqualTo(3);
    }
}
