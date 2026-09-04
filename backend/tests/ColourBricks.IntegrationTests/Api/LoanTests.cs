using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P7-T01 — loan master and disbursement (BRD §48).</summary>
public sealed class LoanTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 3100;

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> CreateProject(HttpClient client) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"Loan P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 50_000_000m, estimatedCost = 40_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> CreateLender(HttpClient client) =>
        (await Json(await client.PostAsJsonAsync(
            "/api/v1/parties?confirm=true", new { name = $"Lender {++_seq}", types = new[] { "Lender" } })))
        .GetProperty("id").GetInt64();

    private async Task<long> AccountId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<decimal> Balance(HttpClient client, long accountId) =>
        (await Json(await client.GetAsync($"/api/v1/accounts/{accountId}"))).GetProperty("balance").GetDecimal();

    private async Task<JsonElement> RecordLoan(HttpClient client, long lenderId, long accountId, long? projectId, decimal principal = 5_000_000m) =>
        await Json(await client.PostAsJsonAsync("/api/v1/loans", new
        {
            lenderId,
            projectId,
            principalAmount = principal,
            annualInterestRatePercent = 9m,
            startDate = "2026-06-01",
            tenureMonths = 60,
            emiStartDate = "2026-07-01",
            disbursementAccountId = accountId,
            disbursementDate = "2026-06-01",
            reference = $"LN-{Guid.NewGuid():N}",
        }));

    [Fact]
    public async Task LoanDisbursement_IsNotProjectIncome()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client);
        long lenderId = await CreateLender(client);
        long bank = await AccountId(client, "HDFC");

        await RecordLoan(client, lenderId, bank, projectId);

        // Revenue on a receipts basis counts only money received as project income.
        JsonElement pnl = await Json(await client.GetAsync(
            $"/api/v1/projects/{projectId}/pnl?revenueBasis=Receipts"));
        pnl.GetProperty("revenue").GetDecimal().Should().Be(0m);
        pnl.GetProperty("actualCost").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task LoanDisbursement_IncreasesBankAndLiability()
    {
        HttpClient client = Client;
        long lenderId = await CreateLender(client);
        long bank = await AccountId(client, "HDFC");

        decimal before = await Balance(client, bank);
        JsonElement loan = await RecordLoan(client, lenderId, bank, projectId: null);

        (await Balance(client, bank)).Should().Be(before + 5_000_000m);
        // The outstanding principal is the liability raised at the lender.
        loan.GetProperty("outstandingPrincipal").GetDecimal().Should().Be(5_000_000m);
    }

    [Fact]
    public async Task OutstandingPrincipal_IsDerived()
    {
        HttpClient client = Client;
        long lenderId = await CreateLender(client);
        long bank = await AccountId(client, "HDFC");

        long loanId = (await RecordLoan(client, lenderId, bank, projectId: null)).GetProperty("id").GetInt64();

        JsonElement fresh = await Json(await client.GetAsync($"/api/v1/loans/{loanId}"));
        fresh.GetProperty("outstandingPrincipal").GetDecimal().Should().Be(fresh.GetProperty("principalAmount").GetDecimal());

        (await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/reverse", new { reason = "test" }))
            .EnsureSuccessStatusCode();

        // Derived from the ledger, not a stored copy of principalAmount: reversing the
        // disbursement nets the loan-payable balance back to zero.
        JsonElement reversed = await Json(await client.GetAsync($"/api/v1/loans/{loanId}"));
        reversed.GetProperty("outstandingPrincipal").GetDecimal().Should().Be(0m);
        reversed.GetProperty("status").GetString().Should().Be("Reversed");
    }

    [Fact]
    public async Task Schedule_Generate_RepaysPrincipalExactly_AndSetsLoanEmiEndDate()
    {
        HttpClient client = Client;
        long lenderId = await CreateLender(client);
        long bank = await AccountId(client, "HDFC");
        long loanId = (await RecordLoan(client, lenderId, bank, projectId: null)).GetProperty("id").GetInt64();

        JsonElement rows = await Json(await client.PostAsJsonAsync(
            $"/api/v1/loans/{loanId}/schedule", new { }));

        rows.GetArrayLength().Should().Be(60);
        rows.EnumerateArray().Sum(r => r.GetProperty("principalComponent").GetDecimal()).Should().Be(5_000_000m);
        rows.EnumerateArray().Last().GetProperty("closingPrincipal").GetDecimal().Should().Be(0m);

        JsonElement loan = await Json(await client.GetAsync($"/api/v1/loans/{loanId}"));
        loan.GetProperty("emiEndDate").GetString().Should().Be("2031-06-01");
        loan.GetProperty("emiAmount").GetDecimal().Should().BeApproximately(103_791.78m, 1m);
    }

    [Fact]
    public async Task Schedule_Regenerate_ReprisesTail_KeepsPrincipalWhole()
    {
        HttpClient client = Client;
        long lenderId = await CreateLender(client);
        long bank = await AccountId(client, "HDFC");
        long loanId = (await RecordLoan(client, lenderId, bank, projectId: null)).GetProperty("id").GetInt64();

        await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/schedule", new { });
        decimal emiBefore = (await Json(await client.GetAsync($"/api/v1/loans/{loanId}/schedule")))
            .EnumerateArray().First().GetProperty("emiAmount").GetDecimal();

        JsonElement rows = await Json(await client.PostAsJsonAsync(
            $"/api/v1/loans/{loanId}/schedule/regenerate", new { newAnnualRatePercent = 11.0m }));

        rows.EnumerateArray().Sum(r => r.GetProperty("principalComponent").GetDecimal()).Should().Be(5_000_000m);
        rows.EnumerateArray().First().GetProperty("emiAmount").GetDecimal().Should().NotBe(emiBefore);
    }
}
