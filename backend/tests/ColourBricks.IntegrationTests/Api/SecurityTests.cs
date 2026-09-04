using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using ColourBricks.Api.Authorization;
using ColourBricks.Infrastructure.Identity;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P9-T03 — the security review checks (BRD §58–§64, §67).</summary>
[Trait("Category", "Security")]
public sealed class SecurityTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 4500;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"SEC P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 9_000_000m, estimatedCost = 7_000_000m,
        }))).GetProperty("id").GetInt64();

    [Fact]
    public void EveryEndpoint_HasPermissionAttribute()
    {
        // Controllers that gate access with [Authorize] + in-code permission checks
        // rather than the [HasPermission] attribute.
        HashSet<string> authorizeOnlyAllowlist = ["AttachmentsController", "AuthController", "DiagnosticsController"];

        var offenders = new List<string>();
        IEnumerable<Type> controllers = typeof(ColourBricks.Api.DependencyInjection).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (Type controller in controllers)
        {
            bool classHasPermission = controller.GetCustomAttribute<HasPermissionAttribute>() is not null;
            bool classAuthorize = controller.GetCustomAttribute<AuthorizeAttribute>() is not null;

            foreach (MethodInfo action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null))
            {
                bool ok = classHasPermission
                    || action.GetCustomAttribute<HasPermissionAttribute>() is not null
                    || action.GetCustomAttribute<AllowAnonymousAttribute>() is not null
                    || (classAuthorize && authorizeOnlyAllowlist.Contains(controller.Name))
                    || action.GetCustomAttribute<AuthorizeAttribute>() is not null && authorizeOnlyAllowlist.Contains(controller.Name);

                if (!ok)
                {
                    offenders.Add($"{controller.Name}.{action.Name}");
                }
            }
        }

        offenders.Should().BeEmpty("every endpoint must carry an explicit permission or anonymous attribute");
    }

    [Fact]
    public async Task Idor_ScopedUser_CannotReadUnassignedProjectData()
    {
        HttpClient admin = Admin;
        long allowed = await Project(admin);
        long forbidden = await Project(admin);

        long userId;
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userId = await db.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstAsync();
            db.UserProjectAccess.Add(new UserProjectAccess { UserId = userId, ProjectId = allowed });
            await db.SaveChangesAsync();
        }

        HttpClient scoped = Factory.CreateClientAs(userId: userId, permissions: "*");

        string[] templates =
        [
            "/api/v1/projects/{0}/dashboard",
            "/api/v1/projects/{0}/pnl",
            "/api/v1/projects/{0}/budget-vs-actual",
            "/api/v1/projects/{0}/financial-ledger",
        ];

        foreach (string template in templates)
        {
            (await scoped.GetAsync(string.Format(template, allowed))).IsSuccessStatusCode
                .Should().BeTrue($"the assigned project is readable: {template}");

            HttpResponseMessage forbiddenResponse = await scoped.GetAsync(string.Format(template, forbidden));
            forbiddenResponse.IsSuccessStatusCode
                .Should().BeFalse($"a scoped user must not read an unassigned project: {template}");
        }
    }

    [Fact]
    public async Task Login_RateLimited()
    {
        HttpClient client = Factory.CreateClient();

        string email = $"brute-{Guid.NewGuid():n}@nowhere.test";
        var statuses = new List<HttpStatusCode>();
        for (int i = 0; i < 20; i++)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password = "wrong-password" });
            statuses.Add(response.StatusCode);
        }

        statuses.Should().Contain(HttpStatusCode.TooManyRequests);
        statuses.Take(10).Should().NotContain(HttpStatusCode.TooManyRequests); // the first window is allowed
    }

    [Fact]
    public async Task SecurityHeaders_Present()
    {
        HttpResponseMessage response = await Admin.GetAsync("/health");

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
    }

    [Fact]
    public async Task Attachment_NotDirectlyAccessible()
    {
        HttpClient anonymous = Factory.CreateClient();

        // The download endpoint requires authentication...
        (await anonymous.GetAsync("/api/v1/attachments/1")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);

        // ...and there is no static-file route serving the attachment store.
        (await anonymous.GetAsync("/attachments/anything.pdf")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await anonymous.GetAsync("/uploads/anything.pdf")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }
}
