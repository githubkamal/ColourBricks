using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P1-T04 — Department master (BRD §8, §9, §70 rules 8, 9).</summary>
public sealed class DepartmentTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");

    [Fact]
    public async Task CreateDepartment_DuplicateName_Returns409()
    {
        HttpClient client = Client;

        (await client.PostAsJsonAsync("/api/v1/departments", new { name = "Waterproofing" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage duplicate =
            await client.PostAsJsonAsync("/api/v1/departments", new { name = "  waterproofing  " });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicate.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using JsonDocument body = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("existingId").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeactivatedDepartment_ExcludedFromPickers_ButPresentInHistory()
    {
        HttpClient client = Client;

        HttpResponseMessage created =
            await client.PostAsJsonAsync("/api/v1/departments", new { name = "Scaffolding" });
        created.EnsureSuccessStatusCode();
        using JsonDocument deptBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        long deptId = deptBody.RootElement.GetProperty("id").GetInt64();
        string stamp = deptBody.RootElement.GetProperty("concurrencyStamp").GetString()!;

        // A team recorded against it — the "history" that must survive deactivation.
        HttpResponseMessage team = await client.PostAsJsonAsync(
            "/api/v1/teams", new { name = "Scaffolding Crew A", departmentId = deptId });
        team.EnsureSuccessStatusCode();
        long teamId = JsonDocument.Parse(await team.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetInt64();

        HttpResponseMessage deactivated = await client.PutAsJsonAsync(
            $"/api/v1/departments/{deptId}",
            new { name = "Scaffolding", isActive = false, concurrencyStamp = stamp });
        deactivated.EnsureSuccessStatusCode();

        // Pickers (default list) hide it.
        List<string> active = await Names(client, "/api/v1/departments");
        active.Should().NotContain("Scaffolding");

        // It is still resolvable when history is shown.
        List<string> all = await Names(client, "/api/v1/departments?includeInactive=true");
        all.Should().Contain("Scaffolding");

        // The team still reports its (now inactive) department — history intact.
        HttpResponseMessage fetched = await client.GetAsync($"/api/v1/teams/{teamId}");
        fetched.EnsureSuccessStatusCode();
        JsonDocument.Parse(await fetched.Content.ReadAsStringAsync())
            .RootElement.GetProperty("departmentName").GetString().Should().Be("Scaffolding");
    }

    private static async Task<List<string>> Names(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.EnumerateArray().Select(d => d.GetProperty("name").GetString()!).ToList();
    }
}
