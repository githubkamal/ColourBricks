using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P1-T07 — Temple master and project donation setup (BRD §26, §70 rules 10, 11).</summary>
public sealed class ProjectDonationTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");

    private async Task<long> CreateProject(HttpClient client, string name, string code, decimal contractValue)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name,
            code,
            status = "Ongoing",
            startDate = "2026-04-01",
            expectedEndDate = "2027-03-31",
            contractValue,
            estimatedCost = contractValue * 0.8m,
        });
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetInt64();
    }

    private async Task<long> CreateTemple(HttpClient client, string name)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/temples?confirm=true", new { name });
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetInt64();
    }

    private static Task<HttpResponseMessage> PutDonation(
        HttpClient client, long projectId, object body) =>
        client.PutAsJsonAsync($"/api/v1/projects/{projectId}/donation", body);

    [Fact]
    public async Task Donation_PercentageBasis_ComputesCorrectAmount()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "Crore Project", "CB-2026-901", 10_000_000m);
        long temple = await CreateTemple(client, "Sri Venkateswara Temple");

        HttpResponseMessage response = await PutDonation(client, projectId, new
        {
            basis = "Percentage",
            percentage = 2m,
            temples = new[] { new { templeId = temple, amount = 200_000m } },
        });

        response.EnsureSuccessStatusCode();
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // BRD §26: 2% of ₹1,00,00,000 = ₹2,00,000.
        body.RootElement.GetProperty("donationAmount").GetDecimal().Should().Be(200_000m);
        body.RootElement.GetProperty("contractValueSnapshot").GetDecimal().Should().Be(10_000_000m);
    }

    [Fact]
    public async Task DonationTempleSplit_NotSummingToTotal_Returns400()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "Split Project", "CB-2026-902", 10_000_000m);
        long templeA = await CreateTemple(client, "Temple Alpha");
        long templeB = await CreateTemple(client, "Temple Beta");

        HttpResponseMessage response = await PutDonation(client, projectId, new
        {
            basis = "Percentage",
            percentage = 2m,
            temples = new[]
            {
                new { templeId = templeA, amount = 100_000m },
                new { templeId = templeB, amount = 50_000m }, // sums to 150,000, not 200,000
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").TryGetProperty("temples", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ContractValueChange_RecomputesPercentageDonation()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "Growing Project", "CB-2026-903", 10_000_000m);
        long templeA = await CreateTemple(client, "Temple One");
        long templeB = await CreateTemple(client, "Temple Two");

        (await PutDonation(client, projectId, new
        {
            basis = "Percentage",
            percentage = 2m,
            temples = new[]
            {
                new { templeId = templeA, amount = 100_000m },
                new { templeId = templeB, amount = 100_000m },
            },
        })).EnsureSuccessStatusCode();

        // Double the contract value.
        HttpResponseMessage projectGet = await client.GetAsync($"/api/v1/projects/{projectId}");
        JsonElement project = JsonDocument.Parse(await projectGet.Content.ReadAsStringAsync()).RootElement;

        HttpResponseMessage updated = await client.PutAsJsonAsync($"/api/v1/projects/{projectId}", new
        {
            name = project.GetProperty("name").GetString(),
            status = "Ongoing",
            startDate = project.GetProperty("startDate").GetString(),
            expectedEndDate = project.GetProperty("expectedEndDate").GetString(),
            contractValue = 20_000_000m,
            estimatedCost = project.GetProperty("estimatedCost").GetDecimal(),
            concurrencyStamp = project.GetProperty("concurrencyStamp").GetString(),
        });
        updated.EnsureSuccessStatusCode();

        HttpResponseMessage donation = await client.GetAsync($"/api/v1/projects/{projectId}/donation");
        donation.EnsureSuccessStatusCode();
        using JsonDocument body = JsonDocument.Parse(await donation.Content.ReadAsStringAsync());

        body.RootElement.GetProperty("donationAmount").GetDecimal().Should().Be(400_000m);
        body.RootElement.GetProperty("contractValueSnapshot").GetDecimal().Should().Be(20_000_000m);
        // Splits still total the old 200,000 — the setup needs review.
        body.RootElement.GetProperty("needsReview").GetBoolean().Should().BeTrue();
    }
}
