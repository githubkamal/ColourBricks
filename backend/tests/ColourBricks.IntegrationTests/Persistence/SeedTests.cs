using System.Security.Cryptography;
using System.Text;
using ColourBricks.Infrastructure.Identity;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Persistence;

/// <summary>
/// P0-T09 — the deterministic seed. Covers the identity data only (RBAC catalogue +
/// the Administrator). Extending it with the masters demo dataset — 4 projects,
/// 6 vendors, 2 teams, 2 bank accounts — is deferred until those entities exist
/// (P1-T01..T06); see the P0-T09 completion note.
/// </summary>
public sealed class SeedTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Seed_ProducesDeterministicDataset()
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        int permissions = await db.Permissions.CountAsync();
        int rolePermissions = await db.RolePermissions.CountAsync();

        List<(string Name, int Grants)> roles = await db.Roles
            .OrderBy(r => r.Name)
            .Select(r => new ValueTuple<string, int>(
                r.Name, db.RolePermissions.Count(rp => rp.RoleId == r.Id)))
            .ToListAsync();

        int users = await db.Users.CountAsync();
        User admin = await db.Users.SingleAsync();

        // Fingerprint of the seed — the row counts and key totals.
        string fingerprint =
            $"permissions={permissions};"
            + $"rolePermissions={rolePermissions};"
            + $"users={users};"
            + "roles=" + string.Join("|", roles.Select(r => $"{r.Name}:{r.Grants}"));

        const string expected =
            "permissions=216;rolePermissions=427;users=1;"
            + "roles=Accounts Team:109|Administrator:216|Data Entry User:14|Management:32|Project Manager:56";

        fingerprint.Should().Be(expected);
        Sha256Hex(fingerprint).Should().Be(Sha256Hex(expected));

        admin.Email.Should().Be("admin@colourbricks.local");
        admin.RoleId.Should().Be(
            await db.Roles.Where(r => r.Name == "Administrator").Select(r => r.Id).SingleAsync());
    }

    private static string Sha256Hex(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
