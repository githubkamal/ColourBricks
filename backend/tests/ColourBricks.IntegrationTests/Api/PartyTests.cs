using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P1-T02 — Party master (BRD §9, §11–13, §26, §70 rules 14, 16, 17).</summary>
public sealed class PartyTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");

    private static Task<HttpResponseMessage> Create(
        HttpClient client, string name, string[] types, bool confirm = false) =>
        client.PostAsJsonAsync(
            $"/api/v1/parties?confirm={confirm.ToString().ToLowerInvariant()}",
            new { name, types });

    [Fact]
    public async Task CreateParty_ExactNormalisedDuplicate_Returns409()
    {
        HttpClient client = Client;

        (await Create(client, "ABC Cement", ["Vendor"], confirm: true))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage duplicate = await Create(client, "abc  cement", ["Vendor"]);

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicate.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task CreateParty_NearDuplicate_ReturnsWarningPayload()
    {
        HttpClient client = Client;

        (await Create(client, "ABC Cement", ["Vendor"], confirm: true))
            .EnsureSuccessStatusCode();

        HttpResponseMessage warned = await Create(client, "ABC Cements", ["Vendor"]);

        warned.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument body = JsonDocument.Parse(await warned.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("requiresConfirmation").GetBoolean().Should().BeTrue();
        JsonElement near = body.RootElement.GetProperty("nearDuplicates");
        near.GetArrayLength().Should().Be(1);
        near[0].GetProperty("name").GetString().Should().Be("ABC Cement");

        // Confirming proceeds.
        (await Create(client, "ABC Cements", ["Vendor"], confirm: true))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SearchParty_RanksPrefixMatchesFirst()
    {
        HttpClient client = Client;
        foreach (string name in new[] { "ABC Cement", "Cement Corner", "Cement" })
        {
            (await Create(client, name, ["Vendor"], confirm: true)).EnsureSuccessStatusCode();
        }

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/search?q=cement");
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        List<string> names = doc.RootElement.EnumerateArray()
            .Select(p => p.GetProperty("name").GetString()!)
            .ToList();

        names.Should().Equal("Cement", "Cement Corner", "ABC Cement");
    }

    [Fact]
    public async Task Party_CanHoldMultipleTypes()
    {
        HttpClient client = Client;

        HttpResponseMessage created = await Create(client, "Multi Co", ["Vendor", "Subcontractor"], confirm: true);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        List<string> types = body.RootElement.GetProperty("types").EnumerateArray()
            .Select(t => t.GetString()!)
            .ToList();
        types.Should().BeEquivalentTo(["Vendor", "Subcontractor"]);

        // Found under either type filter.
        (await SearchNames(client, "/api/v1/parties/search?q=multi&type=Vendor")).Should().Contain("Multi Co");
        (await SearchNames(client, "/api/v1/parties/search?q=multi&type=Subcontractor")).Should().Contain("Multi Co");
    }

    private static async Task<List<string>> SearchNames(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.EnumerateArray().Select(p => p.GetProperty("name").GetString()!).ToList();
    }
}
