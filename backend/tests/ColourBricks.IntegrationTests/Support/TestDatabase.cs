using ColourBricks.Application.Abstractions;
using ColourBricks.Infrastructure.Auditing;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.IntegrationTests.Support;

/// <summary>
/// Builds <see cref="AppDbContext"/> instances against the local MySQL/MariaDB
/// instance used for development (XAMPP on :3306 by default; plan.md §3.1).
/// Override with the <c>COLOURBRICKS_TEST_CONNECTION</c> environment variable in CI.
/// </summary>
/// <remarks>
/// P0-T09 replaces this with the shared Respawn-reset harness. The plan's original
/// Testcontainers approach is not used here: this environment runs no Docker.
/// </remarks>
public static class TestDatabase
{
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("COLOURBRICKS_TEST_CONNECTION")
        ?? "Server=localhost;Port=3306;Database=colourbricks_test;User ID=root;Password=;"
           + "TreatTinyAsBoolean=false;AllowUserVariables=true;UseAffectedRows=false";

    private static readonly Lazy<ServerVersion> Version = new(() => ServerVersion.AutoDetect(ConnectionString));

    /// <summary>The shared connection string with its database name swapped out.</summary>
    public static string ConnectionStringFor(string databaseName)
    {
        var builder = new MySqlConnector.MySqlConnectionStringBuilder(ConnectionString)
        {
            Database = databaseName,
        };
        return builder.ConnectionString;
    }

    public static DbContextOptions<AppDbContext> CreateOptions(string? connectionString = null)
    {
        string connection = connectionString ?? ConnectionString;
        ServerVersion version = connectionString is null ? Version.Value : ServerVersion.AutoDetect(connection);

        return new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(
                connection,
                version,
                mySql => mySql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;
    }

    public static AppDbContext CreateContext() => new(CreateOptions());

    /// <summary>
    /// A context with the full P0-T02/P0-T06 interceptor chain wired and a fixed acting
    /// user, so audit and append-only behaviour can be tested without the web host.
    /// </summary>
    public static AppDbContext CreateContextWithInterceptors(long? currentUserId = null)
    {
        var currentUser = new StubCurrentUser(currentUserId);

        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(
                ConnectionString,
                Version.Value,
                mySql => mySql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .AddInterceptors(
                new AuditableEntitySaveChangesInterceptor(currentUser, TimeProvider.System),
                new AppendOnlyGuardInterceptor(),
                new AuditSaveChangesInterceptor(currentUser, TimeProvider.System))
            .Options;

        return new AppDbContext(options);
    }

    private sealed class StubCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;
    }
}
