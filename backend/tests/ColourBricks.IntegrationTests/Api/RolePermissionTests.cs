using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P1-T09 — Role and permission configuration (BRD §60–63).</summary>
public sealed class RolePermissionTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string Password = "RoleTest!23456";

    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> CreateRole(HttpClient client, string name) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/roles", new { name }))).GetProperty("id").GetInt64();

    private async Task<long> AdministratorRoleId(HttpClient client) =>
        (await Json(await client.GetAsync("/api/v1/roles")))
        .EnumerateArray().First(r => r.GetProperty("name").GetString() == "Administrator")
        .GetProperty("id").GetInt64();

    [Fact]
    public async Task UpdateRolePermissions_WritesAuditEntry()
    {
        HttpClient client = Admin;
        long roleId = await CreateRole(client, $"Auditable Role {Guid.NewGuid():N}");

        (await client.PutAsJsonAsync($"/api/v1/roles/{roleId}/permissions", new
        {
            permissionKeys = new[] { "projects.view", "projects.add" },
        })).EnsureSuccessStatusCode();

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        bool audited = await db.AuditLogs.AnyAsync(a =>
            a.Module == "roles"
            && a.Action == "permissions_updated"
            && a.RecordId == roleId.ToString()
            && a.Details!.Contains("projects.add"));
        audited.Should().BeTrue();
    }

    [Fact]
    public async Task AdministratorRole_CannotLoseCorePermissions()
    {
        HttpClient client = Admin;
        long adminRoleId = await AdministratorRoleId(client);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/v1/roles/{adminRoleId}/permissions", new { permissionKeys = new[] { "dashboard.view" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task PermissionChange_ReflectedAfterTokenRefresh()
    {
        HttpClient admin = Admin;
        string roleName = $"Refresh Role {Guid.NewGuid():N}";
        long roleId = await CreateRole(admin, roleName);

        (await admin.PutAsJsonAsync($"/api/v1/roles/{roleId}/permissions", new
        {
            permissionKeys = new[] { "projects.view" },
        })).EnsureSuccessStatusCode();

        string email = $"roletest+{Guid.NewGuid():N}@colourbricks.test";
        await Factory.CreateUserAsync(email, Password, roleName: roleName);

        HttpClient user = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });

        HttpResponseMessage login = await user.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();

        JsonElement before = await Json(await user.GetAsync("/api/v1/auth/me"));
        string[] permsBefore = before.GetProperty("permissions").EnumerateArray()
            .Select(p => p.GetString()!).ToArray();
        permsBefore.Should().BeEquivalentTo("projects.view");

        // Grant one more permission, then refresh.
        (await admin.PutAsJsonAsync($"/api/v1/roles/{roleId}/permissions", new
        {
            permissionKeys = new[] { "projects.view", "projects.add" },
        })).EnsureSuccessStatusCode();

        (await user.PostAsync("/api/v1/auth/refresh", content: null)).EnsureSuccessStatusCode();

        JsonElement after = await Json(await user.GetAsync("/api/v1/auth/me"));
        string[] permsAfter = after.GetProperty("permissions").EnumerateArray()
            .Select(p => p.GetString()!).ToArray();
        permsAfter.Should().Contain("projects.add");
    }

    [Fact]
    public async Task CreateRole_DuplicateName_Returns409()
    {
        HttpClient client = Admin;
        string name = $"Unique Role {Guid.NewGuid():N}";

        (await client.PostAsJsonAsync("/api/v1/roles", new { name }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await client.PostAsJsonAsync("/api/v1/roles", new { name }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Catalogue_HasEveryModuleAndAction()
    {
        JsonElement catalogue = await Json(await Admin.GetAsync("/api/v1/roles/catalogue"));

        catalogue.GetProperty("modules").GetArrayLength().Should().Be(27);
        catalogue.GetProperty("actions").GetArrayLength().Should().Be(8);
    }
}
