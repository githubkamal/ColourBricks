using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P1-T01 — Project master (BRD §4, §70 rules 1, 2, 31).</summary>
public sealed class ProjectTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");

    private static Dictionary<string, object?> NewProject(
        string name = "Riverside Towers",
        string? code = null,
        string status = "Ongoing",
        string startDate = "2026-04-01",
        string? expectedEndDate = "2027-03-31",
        decimal? estimatedCost = 8_000_000m) => new()
    {
        ["name"] = name,
        ["code"] = code,
        ["status"] = status,
        ["startDate"] = startDate,
        ["expectedEndDate"] = expectedEndDate,
        ["contractValue"] = 10_000_000m,
        ["estimatedCost"] = estimatedCost,
    };

    [Fact]
    public async Task CreateProject_DuplicateCode_Returns400()
    {
        HttpClient client = Client;

        (await client.PostAsJsonAsync("/api/v1/projects", NewProject(code: "CB-2026-999")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage duplicate =
            await client.PostAsJsonAsync("/api/v1/projects", NewProject(name: "Other", code: "CB-2026-999"));

        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        duplicate.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using JsonDocument problem = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").TryGetProperty("code", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateProject_EndBeforeStart_Returns400()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/v1/projects",
            NewProject(startDate: "2026-06-01", expectedEndDate: "2026-05-01"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProject_WithoutEstimatedCost_Returns400()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/v1/projects", NewProject(estimatedCost: null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").TryGetProperty("EstimatedCost", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ListProjects_FilterByStatus_ReturnsOnlyMatching()
    {
        HttpClient client = Client;
        foreach (string status in new[] { "Ongoing", "Completed", "OnHold", "Cancelled" })
        {
            (await client.PostAsJsonAsync("/api/v1/projects", NewProject(name: $"P-{status}", status: status)))
                .EnsureSuccessStatusCode();
        }

        using JsonDocument onHold = await GetJson(client, "/api/v1/projects?status=OnHold");
        JsonElement items = onHold.RootElement.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("status").GetString().Should().Be("OnHold");

        using JsonDocument all = await GetJson(client, "/api/v1/projects?pageSize=50");
        all.RootElement.GetProperty("totalCount").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task CompletedProject_StillReturnedInReportQueries()
    {
        HttpClient client = Client;
        (await client.PostAsJsonAsync("/api/v1/projects", NewProject(name: "Finished", status: "Completed")))
            .EnsureSuccessStatusCode();

        using JsonDocument reporting = await GetJson(client, "/api/v1/projects/reporting");

        reporting.RootElement.EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .Should().Contain("Finished");
    }

    private static async Task<JsonDocument> GetJson(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
