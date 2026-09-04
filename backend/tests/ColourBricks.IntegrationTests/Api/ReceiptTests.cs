using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P2-T02 — Project income and receipts (BRD §6, §34, §70 rule 46).</summary>
public sealed class ReceiptTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> CreateProject(HttpClient client, string code)
    {
        JsonElement p = await Json(await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"Income Project {code}",
            code,
            status = "Ongoing",
            startDate = "2026-04-01",
            expectedEndDate = "2027-03-31",
            contractValue = 20_000_000m,
            estimatedCost = 16_000_000m,
        }));
        return p.GetProperty("id").GetInt64();
    }

    private async Task<long> IdByName(HttpClient client, string url, string name)
    {
        JsonElement list = await Json(await client.GetAsync(url));
        return list.EnumerateArray().First(x => x.GetProperty("name").GetString() == name)
            .GetProperty("id").GetInt64();
    }

    private async Task<decimal> AccountBalance(HttpClient client, long accountId) =>
        (await Json(await client.GetAsync($"/api/v1/accounts/{accountId}")))
        .GetProperty("balance").GetDecimal();

    [Fact]
    public async Task Receipt_WithMultipleProjects_Returns400()
    {
        HttpClient client = Client;
        long modeId = await IdByName(client, "/api/v1/payment-modes", "Cash");

        // "Attach more than one project" — the request has no single projectId.
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/receipts", new
        {
            projectIds = new[] { 1, 2 },
            type = "ClientAdvance",
            date = "2026-05-01",
            amount = 100_000m,
            paymentModeId = modeId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Receipt_PostsDebitToAccount_AndCreditToProject()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-611");
        long modeId = await IdByName(client, "/api/v1/payment-modes", "Cash");
        long accountId = await IdByName(client, "/api/v1/accounts?type=Cash", "Office Cash");

        decimal before = await AccountBalance(client, accountId);

        HttpResponseMessage recorded = await client.PostAsJsonAsync("/api/v1/receipts", new
        {
            projectId,
            type = "ClientAdvance",
            date = "2026-05-01",
            amount = 1_000_000m,
            paymentModeId = modeId,
            accountId,
            description = "Advance",
        });
        await Json(recorded);

        (await AccountBalance(client, accountId)).Should().Be(before + 1_000_000m);

        decimal income = (await Json(await client.GetAsync($"/api/v1/projects/{projectId}/income-total")))
            .GetProperty("total").GetDecimal();
        income.Should().Be(1_000_000m);

        // Actual cost is untouched.
        var breakdown = await Json(await client.GetAsync($"/api/v1/projects/{projectId}/cost-breakdown"));
        breakdown.EnumerateObject().Sum(p => p.Value.GetDecimal()).Should().Be(0m);
    }

    [Fact]
    public async Task Receipt_Reversal_RestoresAccountBalance()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-612");
        long modeId = await IdByName(client, "/api/v1/payment-modes", "Cash");
        long accountId = await IdByName(client, "/api/v1/accounts?type=Cash", "Site Cash");

        decimal before = await AccountBalance(client, accountId);

        JsonElement receipt = await Json(await client.PostAsJsonAsync("/api/v1/receipts", new
        {
            projectId,
            type = "Stage",
            date = "2026-05-10",
            amount = 450_000m,
            paymentModeId = modeId,
            accountId,
        }));
        long receiptId = receipt.GetProperty("id").GetInt64();

        (await AccountBalance(client, accountId)).Should().Be(before + 450_000m);

        HttpResponseMessage reversed = await client.PostAsJsonAsync(
            $"/api/v1/receipts/{receiptId}/reverse", new { reason = "entered twice" });
        reversed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await AccountBalance(client, accountId)).Should().Be(before);
        (await Json(await client.GetAsync($"/api/v1/projects/{projectId}/income-total")))
            .GetProperty("total").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task ReceiptList_FiltersByDateRange()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-613");
        long modeId = await IdByName(client, "/api/v1/payment-modes", "Cash");

        foreach (string date in new[] { "2026-05-01", "2026-06-01", "2026-07-01" })
        {
            (await client.PostAsJsonAsync("/api/v1/receipts", new
            {
                projectId,
                type = "Other",
                date,
                amount = 10_000m,
                paymentModeId = modeId,
            })).EnsureSuccessStatusCode();
        }

        JsonElement inRange = await Json(await client.GetAsync(
            $"/api/v1/projects/{projectId}/receipts?from=2026-05-15&to=2026-06-15"));

        inRange.GetArrayLength().Should().Be(1);
        inRange[0].GetProperty("date").GetString().Should().StartWith("2026-06-01");
    }
}
