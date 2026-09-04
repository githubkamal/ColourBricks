using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Identity;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P1-T08 — User management (BRD §59, §64).</summary>
public sealed class UserManagementTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Xunit.Sdk.XunitException($"HTTP {(int)response.StatusCode}: {body}");
        }

        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> AdminUserId(HttpClient client)
    {
        JsonElement users = await Json(await client.GetAsync("/api/v1/users?includeInactive=true"));
        return users.EnumerateArray()
            .First(u => u.GetProperty("email").GetString() == "admin@colourbricks.local")
            .GetProperty("id").GetInt64();
    }

    private async Task<long> CreateProject(HttpClient client, string name, string code)
    {
        JsonElement project = await Json(await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name,
            code,
            status = "Ongoing",
            startDate = "2026-04-01",
            expectedEndDate = "2027-03-31",
            contractValue = 1_000_000m,
            estimatedCost = 800_000m,
        }));
        return project.GetProperty("id").GetInt64();
    }

    [Fact]
    public async Task DeactivateUser_RevokesRefreshTokens()
    {
        HttpClient client = Admin;
        long userId = await Factory.CreateUserAsync("revoke@example.com", "Passw0rd!23");

        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (int i = 0; i < 2; i++)
            {
                db.RefreshTokens.Add(new RefreshToken
                {
                    UserId = userId,
                    TokenHash = Guid.NewGuid().ToString("N"),
                    FamilyId = Guid.NewGuid(),
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
                });
            }
            await db.SaveChangesAsync();
        }

        (await Factory.RefreshTokenCountsAsync(userId)).Active.Should().Be(2);

        JsonElement user = await Json(await client.GetAsync($"/api/v1/users/{userId}"));
        HttpResponseMessage updated = await client.PutAsJsonAsync($"/api/v1/users/{userId}", new
        {
            name = user.GetProperty("name").GetString(),
            email = user.GetProperty("email").GetString(),
            mobile = (string?)null,
            roleId = (long?)null,
            departmentId = (long?)null,
            isActive = false,
            concurrencyStamp = user.GetProperty("concurrencyStamp").GetString(),
        });
        updated.EnsureSuccessStatusCode();

        (int active, int total) = await Factory.RefreshTokenCountsAsync(userId);
        active.Should().Be(0);
        total.Should().Be(2);
    }

    [Fact]
    public async Task DeleteLastAdministrator_Returns400()
    {
        HttpClient client = Admin;
        long adminId = await AdminUserId(client);

        HttpResponseMessage response = await client.DeleteAsync($"/api/v1/users/{adminId}?reason=Test");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        // The account is untouched.
        (await client.GetAsync($"/api/v1/users/{adminId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeactivateLastAdministrator_Returns400()
    {
        HttpClient client = Admin;
        long adminId = await AdminUserId(client);
        JsonElement admin = await Json(await client.GetAsync($"/api/v1/users/{adminId}"));

        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/v1/users/{adminId}", new
        {
            name = admin.GetProperty("name").GetString(),
            email = admin.GetProperty("email").GetString(),
            mobile = (string?)null,
            roleId = admin.GetProperty("roleId").GetInt64(),
            departmentId = (long?)null,
            isActive = false,
            concurrencyStamp = admin.GetProperty("concurrencyStamp").GetString(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AssignProjects_ScopesSubsequentQueries()
    {
        HttpClient admin = Admin;
        long p1 = await CreateProject(admin, "Alpha Tower", "CB-2026-951");
        long p2 = await CreateProject(admin, "Beta Tower", "CB-2026-952");
        long p3 = await CreateProject(admin, "Gamma Tower", "CB-2026-953");

        long userId = await Factory.CreateUserAsync("scoped@example.com", "Passw0rd!23");

        HttpResponseMessage assigned = await admin.PutAsJsonAsync(
            $"/api/v1/users/{userId}/projects", new { projectIds = new[] { p1, p2 } });
        assigned.EnsureSuccessStatusCode();

        HttpClient scoped = Factory.CreateClientAs(userId, permissions: "projects.view");
        JsonElement list = await Json(await scoped.GetAsync("/api/v1/projects?pageSize=100"));
        long[] visible = list.GetProperty("items").EnumerateArray()
            .Select(p => p.GetProperty("id").GetInt64())
            .ToArray();

        visible.Should().BeEquivalentTo(new[] { p1, p2 });
        visible.Should().NotContain(p3);

        // The administrator is unrestricted.
        JsonElement all = await Json(await admin.GetAsync("/api/v1/projects?pageSize=100"));
        all.GetProperty("items").EnumerateArray()
            .Select(p => p.GetProperty("id").GetInt64())
            .Should().Contain(p3);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_Returns409()
    {
        HttpClient client = Admin;

        (await client.PostAsJsonAsync("/api/v1/users", new
        {
            name = "First",
            email = "dupe@example.com",
            password = "Passw0rd!23",
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage duplicate = await client.PostAsJsonAsync("/api/v1/users", new
        {
            name = "Second",
            email = "DUPE@example.com",
            password = "Passw0rd!23",
        });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicate.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
