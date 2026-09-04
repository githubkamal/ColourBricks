using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P2-T05 — Labour and subcontractor work and payments (BRD §10, §52).</summary>
public sealed class LabourWorkTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> CreateProject(HttpClient client, string code) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"Labour Project {code}",
            code,
            status = "Ongoing",
            startDate = "2026-04-01",
            expectedEndDate = "2027-03-31",
            contractValue = 5_000_000m,
            estimatedCost = 4_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> CreateTeam(HttpClient client, string name)
    {
        JsonElement departments = await Json(await client.GetAsync("/api/v1/departments"));
        long deptId = departments.EnumerateArray().First().GetProperty("id").GetInt64();
        JsonElement team = await Json(await client.PostAsJsonAsync(
            "/api/v1/teams?confirm=true", new { name, departmentId = deptId }));
        return team.GetProperty("id").GetInt64();
    }

    private async Task<long> ModeId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/payment-modes")))
        .EnumerateArray().First(m => m.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<long> AccountId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<decimal> Balance(HttpClient client, long accountId) =>
        (await Json(await client.GetAsync($"/api/v1/accounts/{accountId}"))).GetProperty("balance").GetDecimal();

    private async Task<long> RecordWork(HttpClient client, long projectId, long teamId, decimal agreed) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/labour/work", new
        {
            projectId, teamId, date = "2026-05-01", agreedValue = agreed, workType = "Wiring",
        }))).GetProperty("id").GetInt64();

    private static Task<HttpResponseMessage> Pay(
        HttpClient client, long workId, decimal amount, long modeId, long? accountId, string date = "2026-05-05") =>
        client.PostAsJsonAsync($"/api/v1/labour/work/{workId}/payments", new
        {
            date, amount, frequency = "Weekly", paymentModeId = modeId, accountId,
        });

    private async Task<decimal> Outstanding(HttpClient client, long workId) =>
        (await Json(await client.GetAsync($"/api/v1/labour/work/{workId}"))).GetProperty("outstanding").GetDecimal();

    [Fact]
    public async Task WorkEntry_Outstanding_EqualsAgreedMinusPaid()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-651");
        long teamId = await CreateTeam(client, "Electrical Team A");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");
        long workId = await RecordWork(client, projectId, teamId, 100_000m);

        (await Pay(client, workId, 30_000m, mode, cash)).EnsureSuccessStatusCode();

        (await Outstanding(client, workId)).Should().Be(70_000m);
    }

    [Fact]
    public async Task MultiplePartialPayments_SumCorrectly()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-652");
        long teamId = await CreateTeam(client, "Electrical Team B");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Site Cash");
        long workId = await RecordWork(client, projectId, teamId, 100_000m);

        (await Pay(client, workId, 30_000m, mode, cash, "2026-05-05")).EnsureSuccessStatusCode();
        (await Pay(client, workId, 45_000m, mode, cash, "2026-05-12")).EnsureSuccessStatusCode();

        JsonElement work = await Json(await client.GetAsync($"/api/v1/labour/work/{workId}"));
        work.GetProperty("totalPaid").GetDecimal().Should().Be(75_000m);
        work.GetProperty("outstanding").GetDecimal().Should().Be(25_000m);

        JsonElement statement = await Json(await client.GetAsync($"/api/v1/labour/teams/{teamId}/statement"));
        statement.EnumerateArray().Count(r => r.GetProperty("kind").GetString() == "Payment").Should().Be(2);
    }

    [Fact]
    public async Task Payment_ExceedingAgreedValue_Returns400()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-653");
        long teamId = await CreateTeam(client, "Plumbing Team A");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");
        long workId = await RecordWork(client, projectId, teamId, 100_000m);

        (await Pay(client, workId, 60_000m, mode, cash)).EnsureSuccessStatusCode();

        HttpResponseMessage tooMuch = await Pay(client, workId, 50_000m, mode, cash);
        tooMuch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TeamStatement_ShowsRunningOutstanding()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-654");
        long teamId = await CreateTeam(client, "Electrical Team C");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");
        long workId = await RecordWork(client, projectId, teamId, 100_000m);

        (await Pay(client, workId, 30_000m, mode, cash, "2026-05-05")).EnsureSuccessStatusCode();
        (await Pay(client, workId, 45_000m, mode, cash, "2026-05-12")).EnsureSuccessStatusCode();

        JsonElement statement = await Json(await client.GetAsync($"/api/v1/labour/teams/{teamId}/statement"));
        decimal[] running = statement.EnumerateArray()
            .Select(r => r.GetProperty("runningOutstanding").GetDecimal()).ToArray();

        running.Should().Equal(100_000m, 70_000m, 25_000m);
    }

    [Fact]
    public async Task WorkEntry_PostsExpense_PaymentDoesNot()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-655");
        long teamId = await CreateTeam(client, "Plumbing Team B");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");

        long workId = await RecordWork(client, projectId, teamId, 100_000m);

        JsonElement afterWork = await Json(await client.GetAsync($"/api/v1/projects/{projectId}/cost-breakdown"));
        afterWork.GetProperty("Labour").GetDecimal().Should().Be(100_000m);

        decimal before = await Balance(client, cash);
        (await Pay(client, workId, 40_000m, mode, cash)).EnsureSuccessStatusCode();

        JsonElement afterPay = await Json(await client.GetAsync($"/api/v1/projects/{projectId}/cost-breakdown"));
        afterPay.GetProperty("Labour").GetDecimal().Should().Be(100_000m); // unchanged by the payment
        (await Balance(client, cash)).Should().Be(before - 40_000m);
    }
}
