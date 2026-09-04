using ColourBricks.Infrastructure.Identity;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Respawn;
using Respawn.Graph;

namespace ColourBricks.IntegrationTests.Support;

/// <summary>
/// One <see cref="ColourBricksApiFactory"/> and one <see cref="Respawner"/> for the
/// whole integration test assembly. Migrations are applied once; <see cref="Respawner"/>
/// wipes transactional data before every test so tests are order-independent
/// (plan.md P0-T09). The RBAC catalogue (Permission/Role/RolePermission) is treated
/// as reference data and kept across resets.
/// </summary>
public sealed class IntegrationFixture : IAsyncLifetime
{
    private static readonly string[] ReferenceTables =
    [
        "__EFMigrationsHistory",
        // Permission is a static 216-row catalogue nothing mutates. Role and
        // RolePermission are wiped + re-seeded every reset (P1-T09's role editor
        // mutates them, so tests must not see each other's changes).
        "Permission",
        "Unit",
        "ItemCategory",
        "ExpenseCategory",
    ];

    public ColourBricksApiFactory Factory { get; } = new();

    private Respawner _respawner = null!;

    public async Task InitializeAsync()
    {
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
            await scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>().SeedAsync();
        }

        await using var connection = new MySqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.MySql,
            TablesToIgnore = ReferenceTables.Select(t => new Table(t)).ToArray(),
        });
    }

    /// <summary>Wipes transactional data and re-seeds the Administrator account.</summary>
    public async Task ResetAsync()
    {
        await using (var connection = new MySqlConnection(TestDatabase.ConnectionString))
        {
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);
        }

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
        // Departments are wiped by Respawn (tests rename/deactivate them); restore the
        // BRD §8 defaults. Units/ItemCategory are on the ignore list and survive.
        await scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>().SeedAsync();
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationFixture>
{
    public const string Name = "Integration";
}
