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
    public AppDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("COLOURBRICKS_MIGRATIONS_CONNECTION")
            ?? "Server=localhost;Port=3306;Database=colourbricks;User ID=root;Password=;"
               + "TreatTinyAsBoolean=false;AllowUserVariables=true;UseAffectedRows=false";

        DbContextOptions<AppDbContext> options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseMySql(
                    connectionString,
                    ServerVersion.AutoDetect(connectionString),
                    mySql => mySql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .Options;

        return new AppDbContext(options);
    }
}
