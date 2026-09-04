using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P2-T06 — Temple donation payments (BRD §26, §53).</summary>
public sealed class DonationPaymentTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> CreateProject(HttpClient client, string code, decimal contract) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"Donation Project {code}",
            code,
            status = "Ongoing",
            startDate = "2026-04-01",
            expectedEndDate = "2027-03-31",
            contractValue = contract,
            estimatedCost = contract * 0.8m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> CreateTemple(HttpClient client, string name) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/temples?confirm=true", new { name })))
        .GetProperty("id").GetInt64();

    private async Task<long> ModeId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/payment-modes")))
        .EnumerateArray().First(m => m.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<long> AccountId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    /// <summary>Sets up a ₹1 crore project with a 2% donation split ₹1,20,000 / ₹80,000.</summary>
    private async Task<(long ProjectId, long TempleA, long TempleB)> SetUpDonation(HttpClient client, string code)
    {
        long projectId = await CreateProject(client, code, 10_000_000m);
        long templeA = await CreateTemple(client, $"Temple A {code}");
        long templeB = await CreateTemple(client, $"Temple B {code}");

        (await client.PutAsJsonAsync($"/api/v1/projects/{projectId}/donation", new
        {
            basis = "Percentage",
            percentage = 2m,
            temples = new[]
            {
                new { templeId = templeA, amount = 120_000m },
                new { templeId = templeB, amount = 80_000m },
            },
        })).EnsureSuccessStatusCode();

        return (projectId, templeA, templeB);
    }

    private async Task<JsonElement> Breakdown(HttpClient client, long projectId) =>
        await Json(await client.GetAsync($"/api/v1/projects/{projectId}/cost-breakdown"));

    [Fact]
    public async Task DonationAllocation_CreatesObligation_NotSettlement()
    {
        HttpClient client = Client;
        (long projectId, _, _) = await SetUpDonation(client, "CB-2026-661");

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.Obligations.CountAsync(o =>
            o.Type == ObligationType.TempleDonation && o.ProjectId == projectId)).Should().Be(1);
        (await db.Settlements.CountAsync(s => s.ProjectId == projectId)).Should().Be(0);
    }

    [Fact]
    public async Task DonationPayment_DoesNotDoubleCountExpense()
    {
        HttpClient client = Client;
        (long projectId, long templeA, _) = await SetUpDonation(client, "CB-2026-662");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");

        decimal donationExpenseBefore = (await Breakdown(client, projectId))
            .GetProperty("Temple Donations").GetDecimal();
        donationExpenseBefore.Should().Be(200_000m);

        (await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/donation/temples/{templeA}/payments", new
            {
                date = "2026-05-10", amount = 50_000m, paymentModeId = mode, accountId = cash,
            })).EnsureSuccessStatusCode();

        (await Breakdown(client, projectId)).GetProperty("Temple Donations").GetDecimal()
            .Should().Be(200_000m); // unchanged by the payment
    }

    [Fact]
    public async Task DonationPayment_ExceedingAllocation_Returns400()
    {
        HttpClient client = Client;
        (long projectId, _, long templeB) = await SetUpDonation(client, "CB-2026-663");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/donation/temples/{templeB}/payments", new
            {
                date = "2026-05-10", amount = 90_000m, paymentModeId = mode, accountId = cash, // allocation is 80,000
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DonationOutstanding_ByTempleAndProject()
    {
        HttpClient client = Client;
        (long projectId, long templeA, long templeB) = await SetUpDonation(client, "CB-2026-664");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");

        // Temple A paid fully; Temple B paid partially.
        (await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/donation/temples/{templeA}/payments",
            new { date = "2026-05-10", amount = 120_000m, paymentModeId = mode, accountId = cash }))
            .EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/donation/temples/{templeB}/payments",
            new { date = "2026-05-11", amount = 30_000m, paymentModeId = mode, accountId = cash }))
            .EnsureSuccessStatusCode();

        JsonElement outstanding = await Json(await client.GetAsync(
            $"/api/v1/projects/{projectId}/donation/outstanding"));

        var byTemple = outstanding.EnumerateArray()
            .ToDictionary(x => x.GetProperty("templeId").GetInt64(),
                x => x.GetProperty("outstanding").GetDecimal());

        byTemple[templeA].Should().Be(0m);
        byTemple[templeB].Should().Be(50_000m);
    }
}
