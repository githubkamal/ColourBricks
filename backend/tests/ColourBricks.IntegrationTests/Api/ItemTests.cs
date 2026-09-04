using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P1-T03 — Item and item category master (BRD §14, §15, §70 rules 15–17).</summary>
public sealed class ItemTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");

    private static Task<HttpResponseMessage> Create(
        HttpClient client,
        string name,
        string unit = "Bag",
        decimal defaultRate = 400m,
        decimal taxRate = 18m,
        bool confirm = false) =>
        client.PostAsJsonAsync(
            $"/api/v1/items?confirm={confirm.ToString().ToLowerInvariant()}",
            new { name, unit, defaultRate, taxRate });

    [Fact]
    public async Task CreateItem_DuplicateNormalisedName_Returns409()
    {
        HttpClient client = Client;

        (await Create(client, "TMT Steel", confirm: true))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage duplicate = await Create(client, "  tmt   steel ");

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicate.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using JsonDocument body = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("existingId").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateItem_NearDuplicate_ReturnsWarningThenConfirms()
    {
        HttpClient client = Client;

        (await Create(client, "Cement", confirm: true)).EnsureSuccessStatusCode();

        HttpResponseMessage warned = await Create(client, "Cements");

        warned.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument body = JsonDocument.Parse(await warned.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("requiresConfirmation").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("nearDuplicates")[0].GetProperty("name").GetString()
            .Should().Be("Cement");

        (await Create(client, "Cements", confirm: true))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateItem_UnknownUnit_Returns400()
    {
        HttpResponseMessage response = await Create(Client, "Mystery Powder", unit: "Furlong", confirm: true);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchItem_ReturnsPrefillFields_RankedPrefixFirst()
    {
        HttpClient client = Client;
        foreach (string name in new[] { "White Cement", "Cement Primer", "Cement" })
        {
            (await Create(client, name, unit: "Bag", defaultRate: 395m, taxRate: 28m, confirm: true))
                .EnsureSuccessStatusCode();
        }

        HttpResponseMessage response = await client.GetAsync("/api/v1/items/search?q=cement");
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement[] rows = doc.RootElement.EnumerateArray().ToArray();

        rows.Select(r => r.GetProperty("name").GetString())
            .Should().Equal("Cement", "Cement Primer", "White Cement");

        // Every result carries the fields the picker prefills onto a purchase line.
        rows[0].GetProperty("unit").GetString().Should().Be("Bag");
        rows[0].GetProperty("defaultRate").GetDecimal().Should().Be(395m);
        rows[0].GetProperty("taxRate").GetDecimal().Should().Be(28m);
    }

    [Fact]
    public async Task UpdateItemDefaultRate_ChangesMasterOnly()
    {
        HttpClient client = Client;

        HttpResponseMessage created = await Create(client, "River Sand", unit: "Load", defaultRate: 15000m, confirm: true);
        created.EnsureSuccessStatusCode();
        using JsonDocument createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        long id = createdBody.RootElement.GetProperty("id").GetInt64();
        string stamp = createdBody.RootElement.GetProperty("concurrencyStamp").GetString()!;

        HttpResponseMessage updated = await client.PutAsJsonAsync(
            $"/api/v1/items/{id}",
            new
            {
                name = "River Sand",
                unit = "Load",
                defaultRate = 16500m,
                taxRate = 5m,
                isActive = true,
                concurrencyStamp = stamp,
            });
        updated.EnsureSuccessStatusCode();

        HttpResponseMessage fetched = await client.GetAsync($"/api/v1/items/{id}");
        using JsonDocument body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("defaultRate").GetDecimal().Should().Be(16500m);
    }
}
