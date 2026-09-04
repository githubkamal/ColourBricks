using ColourBricks.Application.Abstractions;
using ColourBricks.Infrastructure.Identity;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Support;

/// <summary>
/// Boots the real API against the local test database (P0-T09). Real cookie login
/// still works; a test may also impersonate a user with the <c>X-Test-Sub</c> /
/// <c>X-Test-Permissions</c> headers (see <see cref="TestAuthHandler"/>).
/// </summary>
public sealed class ColourBricksApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", TestDatabase.ConnectionString);
        builder.UseSetting("Diagnostics:Enabled", "true");
        builder.UseSetting("Auth:MaxFailedAttempts", "3");
        builder.UseSetting("Auth:LockoutMinutes", "15");
        // Attachments (P2-T08): a throwaway storage root and a small size cap so the
        // oversize test doesn't need a multi-MB payload.
        builder.UseSetting("Storage:Root", Path.Combine(Path.GetTempPath(), "cb-test-attachments"));
        builder.UseSetting("Storage:MaxBytes", "65536");
        // TestServer runs plain HTTP, so Secure cookies would never come back.
        builder.UseSetting("Auth:CookieSecure", "false");

        builder.ConfigureTestServices(services =>
        {
            // Default authentication becomes a policy scheme that forwards to the
            // test handler when the impersonation header is present, otherwise to the
            // real JWT-cookie handler.
            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = "Smart";
                    options.DefaultChallengeScheme = "Smart";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { })
                .AddPolicyScheme("Smart", "Smart", options =>
                {
                    options.ForwardDefaultSelector = context =>
                        context.Request.Headers.ContainsKey(TestAuthHandler.SubHeader)
                            ? TestAuthHandler.SchemeName
                            : JwtBearerDefaults.AuthenticationScheme;
                });
        });
    }

    /// <summary>An <see cref="HttpClient"/> that impersonates the given user on every request.</summary>
    public HttpClient CreateClientAs(long userId, string permissions = "*")
    {
        HttpClient client = CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, permissions);
        return client;
    }

    /// <summary>Inserts a user straight into the test database and returns its id.</summary>
    public async Task<long> CreateUserAsync(
        string email, string password, bool isActive = true, string? roleName = null)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        long? roleId = roleName is null
            ? null
            : await db.Roles.Where(r => r.Name == roleName).Select(r => (long?)r.Id).FirstAsync();

        var user = new User
        {
            Name = "Test User",
            Email = email,
            PasswordHash = hasher.Hash(password),
            IsActive = isActive,
            RoleId = roleId,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    public async Task GrantProjectAccessAsync(long userId, params long[] projectIds)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (long projectId in projectIds)
        {
            db.UserProjectAccess.Add(new UserProjectAccess { UserId = userId, ProjectId = projectId });
        }

        await db.SaveChangesAsync();
    }

    public async Task<int> RolePermissionCountAsync(string roleName)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.RolePermissions.CountAsync(rp => rp.Role!.Name == roleName);
    }

    public async Task<int> PermissionCatalogueCountAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Permissions.CountAsync();
    }

    public async Task<IReadOnlySet<string>> RolePermissionKeysAsync(string roleName)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        List<string> keys = await db.RolePermissions
            .Where(rp => rp.Role!.Name == roleName)
            .Select(rp => rp.Permission!.Key)
            .ToListAsync();
        return keys.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<(int Active, int Total)> RefreshTokenCountsAsync(long userId)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        int total = await db.RefreshTokens.CountAsync(t => t.UserId == userId);
        int active = await db.RefreshTokens.CountAsync(t =>
            t.UserId == userId && t.RevokedAtUtc == null && t.ConsumedAtUtc == null && t.ExpiresAtUtc > now);
        return (active, total);
    }
}
