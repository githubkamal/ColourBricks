using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Identity;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P0-T05 — Permission model and policy authorisation (plan.md §9, BRD §58–64).</summary>
public sealed class AuthorizationTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string Password = "Sup3r!Secret";

    [Fact]
    public async Task HasPermission_UserWithoutPermission_Returns403()
    {
        // Data Entry User has no reports.view permission.
        string email = Email();
        await Factory.CreateUserAsync(email, Password, roleName: "Data Entry User");
        HttpClient client = await AuthedClient(email);

        HttpResponseMessage response = await client.GetAsync("/api/v1/_diagnostics/secure-view");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HasPermission_AdminRole_HasAllPermissions()
    {
        int adminGrants = await Factory.RolePermissionCountAsync("Administrator");
        int catalogue = await Factory.PermissionCatalogueCountAsync();
        adminGrants.Should().Be(catalogue).And.BeGreaterThan(0);

        string email = Email();
        await Factory.CreateUserAsync(email, Password, roleName: "Administrator");
        HttpClient client = await AuthedClient(email);

        (await client.GetAsync("/api/v1/_diagnostics/secure-view")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProjectScope_RestrictedUser_SeesOnlyAssignedProjects()
    {
        string email = Email();
        long userId = await Factory.CreateUserAsync(email, Password, roleName: "Project Manager");
        await Factory.GrantProjectAccessAsync(userId, 101, 202);
        HttpClient client = await AuthedClient(email);

        using JsonDocument scope = await GetJson(client, "/api/v1/_diagnostics/project-scope");

        scope.RootElement.GetProperty("unrestricted").GetBoolean().Should().BeFalse();
        scope.RootElement.GetProperty("projectIds").EnumerateArray()
            .Select(e => e.GetInt64()).Should().Equal(101L, 202L);
    }

    [Fact]
    public async Task ProjectScope_UnrestrictedUser_SeesAll()
    {
        string email = Email();
        await Factory.CreateUserAsync(email, Password, roleName: "Accounts Team");
        HttpClient client = await AuthedClient(email);

        using JsonDocument scope = await GetJson(client, "/api/v1/_diagnostics/project-scope");

        scope.RootElement.GetProperty("unrestricted").GetBoolean().Should().BeTrue();
        scope.RootElement.GetProperty("projectIds").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task PermissionSeed_MatchesBrdMatrix()
    {
        PermissionMatrix matrix = PermissionMatrix.Load();

        (await Factory.PermissionCatalogueCountAsync())
            .Should().Be(matrix.Modules.Count * matrix.Actions.Count);

        foreach (PermissionMatrix.RoleDefinition role in matrix.Roles)
        {
            IReadOnlySet<string> seeded = await Factory.RolePermissionKeysAsync(role.Name);
            seeded.Should().BeEquivalentTo(
                matrix.KeysFor(role),
                $"role '{role.Name}' must be seeded exactly as the checked-in matrix says");
        }
    }

    private async Task<HttpClient> AuthedClient(string email)
    {
        HttpClient client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false,
        });

        HttpResponseMessage login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();

        string access = SetCookieHeaderValue
            .ParseList(login.Headers.GetValues("Set-Cookie").ToList())
            .Single(c => c.Name == "cb_access").Value.ToString();

        client.DefaultRequestHeaders.Add("Cookie", $"cb_access={access}");
        return client;
    }

    private static async Task<JsonDocument> GetJson(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string Email() => $"authztest+{Guid.NewGuid():N}@colourbricks.test";
}
