using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P7-T03 — EMI payments: principal vs interest, prepayment, overdue (BRD §48).</summary>
public sealed class LoanEmiPaymentTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 3300;

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> CreateProject(HttpClient client) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"Emi P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2029-03-31",
            contractValue = 80_000_000m, estimatedCost = 60_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> CreateLender(HttpClient client) =>
        (await Json(await client.PostAsJsonAsync(
            "/api/v1/parties?confirm=true", new { name = $"Emi Lender {++_seq}", types = new[] { "Lender" } })))
        .GetProperty("id").GetInt64();

    private async Task<long> AccountId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<long> ModeId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/payment-modes")))
        .EnumerateArray().First(m => m.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<decimal> ActualCost(HttpClient client, long projectId) =>
        (await Json(await client.GetAsync($"/api/v1/projects/{projectId}/budget-vs-actual")))
        .GetProperty("actualCost").GetDecimal();

    private async Task<decimal> Outstanding(HttpClient client, long loanId) =>
        (await Json(await client.GetAsync($"/api/v1/loans/{loanId}"))).GetProperty("outstandingPrincipal").GetDecimal();

    private async Task<(long LoanId, long ProjectId, JsonElement Schedule)> LoanWithSchedule(
        HttpClient client, decimal principal = 5_000_000m)
    {
        long projectId = await CreateProject(client);
        long lenderId = await CreateLender(client);
        long bank = await AccountId(client, "HDFC");

        long loanId = (await Json(await client.PostAsJsonAsync("/api/v1/loans", new
        {
            lenderId, projectId,
            principalAmount = principal, annualInterestRatePercent = 9m,
            startDate = "2026-06-01", tenureMonths = 60, emiStartDate = "2026-07-01",
            disbursementAccountId = bank, disbursementDate = "2026-06-01",
        }))).GetProperty("id").GetInt64();

        JsonElement schedule = await Json(await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/schedule", new { }));
        return (loanId, projectId, schedule);
    }

    private static (long Id, decimal Principal, decimal Interest) Row(JsonElement schedule, int no)
    {
        JsonElement r = schedule.EnumerateArray().First(x => x.GetProperty("instalmentNo").GetInt32() == no);
        return (r.GetProperty("id").GetInt64(),
            r.GetProperty("principalComponent").GetDecimal(),
            r.GetProperty("interestComponent").GetDecimal());
    }

    private Task<HttpResponseMessage> PayEmi(HttpClient client, long loanId, long instalmentId, long mode, long acct) =>
        client.PostAsJsonAsync($"/api/v1/loans/{loanId}/emi-payments", new
        {
            instalmentId, date = "2026-09-01", paymentModeId = mode, accountId = acct,
        });

    [Fact]
    public async Task EmiPayment_InterestIsExpense_PrincipalIsNot()
    {
        HttpClient client = Client;
        long cash = await ModeId(client, "Cash");
        long bank = await AccountId(client, "HDFC");
        (long loanId, long projectId, JsonElement schedule) = await LoanWithSchedule(client);

        var first = Row(schedule, 1);
        decimal costBefore = await ActualCost(client, projectId);
        decimal outstandingBefore = await Outstanding(client, loanId);

        (await PayEmi(client, loanId, first.Id, cash, bank)).EnsureSuccessStatusCode();

        // Only the interest slice hits project cost; the principal slice does not.
        (await ActualCost(client, projectId)).Should().Be(costBefore + first.Interest);
        (await Outstanding(client, loanId)).Should().Be(outstandingBefore - first.Principal);
    }

    [Fact]
    public async Task EmiPayment_ReducesOutstandingPrincipal_ByPrincipalComponentOnly()
    {
        HttpClient client = Client;
        long cash = await ModeId(client, "Cash");
        long bank = await AccountId(client, "HDFC");
        (long loanId, long projectId, JsonElement schedule) = await LoanWithSchedule(client);

        decimal costBefore = await ActualCost(client, projectId);
        decimal principalSum = 0m, interestSum = 0m;
        for (int no = 1; no <= 3; no++)
        {
            var row = Row(schedule, no);
            principalSum += row.Principal;
            interestSum += row.Interest;
            (await PayEmi(client, loanId, row.Id, cash, bank)).EnsureSuccessStatusCode();
        }

        (await Outstanding(client, loanId)).Should().Be(5_000_000m - principalSum);
        (await ActualCost(client, projectId)).Should().Be(costBefore + interestSum);
    }

    [Fact]
    public async Task EmiPrepayment_RegeneratesRemainingSchedule()
    {
        HttpClient client = Client;
        long cash = await ModeId(client, "Cash");
        long bank = await AccountId(client, "HDFC");
        (long loanId, _, JsonElement schedule) = await LoanWithSchedule(client);

        decimal emiBefore = Row(schedule, 5).Principal + Row(schedule, 5).Interest;

        (await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/prepayments", new
        {
            amount = 1_000_000m, date = "2026-09-01", paymentModeId = cash, accountId = bank,
        })).EnsureSuccessStatusCode();

        (await Outstanding(client, loanId)).Should().Be(4_000_000m);

        JsonElement after = await Json(await client.GetAsync($"/api/v1/loans/{loanId}/schedule"));
        after.GetArrayLength().Should().Be(60);
        // The regenerated tail amortises only the ₹40,00,000 still outstanding.
        after.EnumerateArray().Sum(r => r.GetProperty("principalComponent").GetDecimal()).Should().Be(4_000_000m);
        decimal emiAfter = after.EnumerateArray()
            .First(r => r.GetProperty("instalmentNo").GetInt32() == 30).GetProperty("emiAmount").GetDecimal();
        emiAfter.Should().BeLessThan(emiBefore); // lower balance, same remaining count -> smaller EMI
    }

    [Fact]
    public async Task EmiOverdue_PostsNothing()
    {
        HttpClient client = Client;
        (long loanId, long projectId, JsonElement schedule) = await LoanWithSchedule(client);

        // The first instalment fell due 2026-07-01, well before today, and is unpaid.
        JsonElement fresh = await Json(await client.GetAsync($"/api/v1/loans/{loanId}/schedule"));
        fresh.EnumerateArray().First().GetProperty("overdue").GetBoolean().Should().BeTrue();

        (await ActualCost(client, projectId)).Should().Be(0m);
        (await Json(await client.GetAsync($"/api/v1/loans/{loanId}/emi-payments"))).GetArrayLength().Should().Be(0);
        _ = schedule;
    }
}
