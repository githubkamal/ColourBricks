using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ColourBricks.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <c>dotnet ef</c>. Keeps migration tooling independent of
/// the API host wiring. Connection string comes from
/// <c>COLOURBRICKS_MIGRATIONS_CONNECTION</c> or falls back to the local dev database.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// What <see cref="ServerVersion.AutoDetect"/> would report against the deployed
    /// database (deploy/scripts/server-setup.sh provisions MySQL 8). Used only when
    /// no server is reachable, so <c>dotnet ef migrations add</c> works on a machine
    /// with no local MySQL — writing a migration needs the model, not a database.
    /// </summary>
    private static readonly ServerVersion FallbackVersion =
        new MySqlServerVersion(new Version(8, 0, 36));

    public AppDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("COLOURBRICKS_MIGRATIONS_CONNECTION")
            ?? "Server=localhost;Port=3306;Database=colourbricks;User ID=root;Password=;"
               + "TreatTinyAsBoolean=false;AllowUserVariables=true;UseAffectedRows=false";

        ServerVersion version;
        try
        {
            version = ServerVersion.AutoDetect(connectionString);
        }
        catch (Exception)
        {
            version = FallbackVersion;
        }

        DbContextOptions<AppDbContext> options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseMySql(
                    connectionString,
                    version,
                    mySql => mySql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .Options;

        return new AppDbContext(options);
    }
}
