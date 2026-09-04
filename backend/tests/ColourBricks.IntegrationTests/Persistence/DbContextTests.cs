using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ColourBricks.IntegrationTests.Persistence;

/// <summary>
/// P0-T02 — EF Core setup and model conventions (plan.md §5.4, §6, §3.1).
/// All tests in one class so they run serially against the shared local schema.
/// </summary>
public sealed class DbContextTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task DbContext_CanConnect_ToMySql8()
    {
        await using AppDbContext db = TestDatabase.CreateContext();

        (await db.Database.CanConnectAsync()).Should().BeTrue();
    }

    [Fact]
    public void Decimal_Columns_HavePrecision18Scale3()
    {
        // Scale is Money.Scale (client request, 2026-09-04 — raised from 2 to 3), asserted
        // against the constant itself so this test and the real precision can't drift apart.
        using AppDbContext db = TestDatabase.CreateContext();

        IEnumerable<IProperty> decimals = db.Model
            .GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(p => Unwrap(p.ClrType) == typeof(decimal));

        foreach (IProperty property in decimals)
        {
            string name = $"{property.DeclaringType.ShortName()}.{property.Name}";
            property.GetPrecision().Should().Be(18, "decimal {0} must be DECIMAL(18,{1})", name, Money.Scale);
            property.GetScale().Should().Be(Money.Scale, "decimal {0} must be DECIMAL(18,{1})", name, Money.Scale);
        }
    }

    [Fact]
    public void Mapped_Model_HasNoFloatOrDoubleColumns()
    {
        using AppDbContext db = TestDatabase.CreateContext();

        IEnumerable<string> offenders = db.Model
            .GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(p => Unwrap(p.ClrType) == typeof(float) || Unwrap(p.ClrType) == typeof(double))
            .Select(p => $"{p.DeclaringType.ShortName()}.{p.Name}");

        offenders.Should().BeEmpty("plan.md §5.4 bans float/double for mapped columns");
    }

    [Fact]
    public async Task Migration_Down_RevertsCleanly()
    {
        // A throwaway database so reverting every migration can't disturb the
        // schema the other integration tests rely on.
        string connectionString = TestDatabase.ConnectionStringFor("colourbricks_migration_down");
        await using AppDbContext db = new(TestDatabase.CreateOptions(connectionString));
        await db.Database.EnsureDeletedAsync();

        try
        {
            IMigrator migrator = db.Database.GetService<IMigrator>();

            await migrator.MigrateAsync();
            await migrator.MigrateAsync(Migration.InitialDatabase); // revert every migration
            await migrator.MigrateAsync(); // re-apply so the schema is left usable

            (await db.Database.CanConnectAsync()).Should().BeTrue();
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static Type Unwrap(Type clrType) => Nullable.GetUnderlyingType(clrType) ?? clrType;
}
