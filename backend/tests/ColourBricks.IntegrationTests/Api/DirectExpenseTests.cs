using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P2-T04 — Direct project expenses (BRD §7, §5).</summary>
public sealed class DirectExpenseTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
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
            name = $"Expense Project {code}",
            code,
            status = "Ongoing",
            startDate = "2026-04-01",
            expectedEndDate = "2027-03-31",
            contractValue = 5_000_000m,
            estimatedCost = 4_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> CategoryId(HttpClient client, string slug) =>
        (await Json(await client.GetAsync("/api/v1/expense-categories")))
        .EnumerateArray().First(c => c.GetProperty("slug").GetString() == slug).GetProperty("id").GetInt64();

    private async Task<long> AccountId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<long> ModeId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/payment-modes")))
        .EnumerateArray().First(m => m.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<decimal> Balance(HttpClient client, long accountId) =>
        (await Json(await client.GetAsync($"/api/v1/accounts/{accountId}"))).GetProperty("balance").GetDecimal();

    private async Task<JsonElement> Breakdown(HttpClient client, long projectId) =>
        await Json(await client.GetAsync($"/api/v1/projects/{projectId}/cost-breakdown"));

    [Fact]
    public async Task Expense_Categorised_AppearsInCorrectBreakdownBucket()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-641");
        long electrical = await CategoryId(client, "electrical");

        (await client.PostAsJsonAsync("/api/v1/project-expenses", new
        {
            projectId, categoryId = electrical, date = "2026-05-01", amount = 12_500m,
            description = "Site wiring rework",
        })).EnsureSuccessStatusCode();

        (await Breakdown(client, projectId)).GetProperty("Electrical").GetDecimal().Should().Be(12_500m);
    }

    [Fact]
    public async Task Expense_PaidImmediately_ReducesAccount()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-642");
        long other = await CategoryId(client, "other_expenses");
        long cash = await AccountId(client, "Office Cash");
        long mode = await ModeId(client, "Cash");

        decimal before = await Balance(client, cash);

        (await client.PostAsJsonAsync("/api/v1/project-expenses", new
        {
            projectId, categoryId = other, date = "2026-05-02", amount = 3_000m,
            paidImmediately = true, paymentModeId = mode, accountId = cash,
        })).EnsureSuccessStatusCode();

        (await Balance(client, cash)).Should().Be(before - 3_000m);
    }

    [Fact]
    public async Task Expense_Payable_DoesNotReduceAccount()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-643");
        long other = await CategoryId(client, "other_expenses");
        long cash = await AccountId(client, "Site Cash");

        decimal before = await Balance(client, cash);

        (await client.PostAsJsonAsync("/api/v1/project-expenses", new
        {
            projectId, categoryId = other, date = "2026-05-03", amount = 8_000m,
        })).EnsureSuccessStatusCode();

        (await Balance(client, cash)).Should().Be(before);
    }

    [Fact]
    public async Task ExpenseBreakdown_SumsToTotalExpenses()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-644");

        (string slug, decimal amount)[] entries =
        [
            ("labour", 40_000m),
            ("plumbing", 15_000m),
            ("materials", 60_000m),
            ("other_expenses", 5_000m),
        ];

        foreach ((string slug, decimal amount) in entries)
        {
            long categoryId = await CategoryId(client, slug);
            (await client.PostAsJsonAsync("/api/v1/project-expenses", new
            {
                projectId, categoryId, date = "2026-05-04", amount,
            })).EnsureSuccessStatusCode();
        }

        JsonElement breakdown = await Breakdown(client, projectId);
        decimal breakdownTotal = breakdown.EnumerateObject().Sum(p => p.Value.GetDecimal());

        breakdownTotal.Should().Be(entries.Sum(e => e.amount)); // 1,20,000 — no uncategorised residue
    }
}
