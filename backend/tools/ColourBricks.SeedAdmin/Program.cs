using System.Security.Cryptography;
using ColourBricks.Application.Abstractions;
using ColourBricks.Infrastructure;
using ColourBricks.Infrastructure.Identity;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Creates (or resets the password of) an Administrator account directly in the
// database — for production, where IdentitySeeder's Auth:Seed config is
// deliberately absent (appsettings.Development.json only), so nothing gets
// auto-seeded there. Reuses the exact same password hasher (ASP.NET Core
// Identity's PasswordHasher<User>, PBKDF2) and DB wiring (AddInfrastructure)
// as the real app, so a hash produced here is one the API will accept.
//
// Usage (run from the repo's backend/ folder, pointed at the target database
// via the same config the API itself reads — appsettings.{ASPNETCORE_ENVIRONMENT}.json
// and/or standard ASP.NET Core env vars, e.g. ConnectionStrings__Default):
//
//   dotnet run --project tools/ColourBricks.SeedAdmin -- --email you@company.com
//   dotnet run --project tools/ColourBricks.SeedAdmin -- --email you@company.com --password "a specific one"
//
// Omitting --password generates a strong random one, printed once — copy it
// immediately; it is not stored anywhere in plaintext. Sign in and change it
// right away. Re-running against an existing email resets that user's
// password and Administrator role — safe to use later if you're locked out.
//
// Add --sql to print a ready-to-run MySQL statement instead of writing to a
// database — no connection string needed for this mode. The password hash is
// still computed with the real PBKDF2 hasher (MySQL itself can't do this),
// but the resulting SQL can be handed to a DBA to run directly, or against a
// database this machine can't reach:
//
//   dotnet run --project tools/ColourBricks.SeedAdmin -- --email you@company.com --sql

string? email = GetArg(args, "--email");
string? password = GetArg(args, "--password");
string name = GetArg(args, "--name") ?? "Administrator";
bool sqlOnly = args.Contains("--sql");

if (string.IsNullOrWhiteSpace(email))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools/ColourBricks.SeedAdmin -- --email you@company.com [--password \"...\"] [--name \"Full Name\"] [--sql]");
    Console.Error.WriteLine("If --password is omitted, a strong one is generated and printed once.");
    return 1;
}

bool generated = false;
if (string.IsNullOrWhiteSpace(password))
{
    password = GenerateStrongPassword();
    generated = true;
}

if (sqlOnly)
{
    string sqlHash = new IdentityPasswordHasher(new PasswordHasher<User>()).Hash(password);
    Console.WriteLine(BuildSql(email, name, sqlHash));
    if (generated)
    {
        Console.WriteLine();
        Console.WriteLine("-- Password (shown once — copy it now; it is not stored anywhere in plaintext):");
        Console.WriteLine($"-- {password}");
    }

    return 0;
}

// HostApplicationBuilder reads config exactly like the real API does:
// appsettings.json + appsettings.{Environment}.json + environment variables.
// Two adjustments from its defaults: ContentRootPath is pinned to this tool's
// own output directory (where its linked copies of the API's appsettings
// files land), not the current working directory, so it works the same
// regardless of where `dotnet run`/the published exe is invoked from; and the
// environment name is read from ASPNETCORE_ENVIRONMENT — the generic host
// normally reads DOTNET_ENVIRONMENT instead, but the API is an ASP.NET Core
// app and reads ASPNETCORE_ENVIRONMENT, so this tool honours the same
// variable an operator would already have set for the API.
HostApplicationBuilder builder = Host.CreateApplicationBuilder(
    new HostApplicationBuilderSettings
    {
        ContentRootPath = AppContext.BaseDirectory,
        EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
    });
builder.Services.AddInfrastructure(builder.Configuration);
using IHost host = builder.Build();
await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

long? adminRoleId = await db.Roles
    .Where(r => r.Name == IdentitySeeder.AdministratorRoleName)
    .Select(r => (long?)r.Id)
    .FirstOrDefaultAsync();

if (adminRoleId is null)
{
    Console.Error.WriteLine(
        "No 'Administrator' role found. Start the API once against this database first (it seeds the " +
        "RBAC catalogue on startup even with no Auth:Seed configured), then re-run this tool.");
    return 1;
}

string hash = hasher.Hash(password);
User? existing = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

if (existing is null)
{
    db.Users.Add(new User
    {
        Name = name,
        Email = email,
        PasswordHash = hash,
        RoleId = adminRoleId,
        IsActive = true,
    });
    await db.SaveChangesAsync();
    Console.WriteLine($"Created administrator '{email}'.");
}
else
{
    existing.PasswordHash = hash;
    existing.RoleId = adminRoleId;
    existing.IsActive = true;
    await db.SaveChangesAsync();
    Console.WriteLine($"Reset the password and Administrator role for existing user '{email}'.");
}

if (generated)
{
    Console.WriteLine();
    Console.WriteLine("Password (shown once — copy it now, sign in, then change it immediately):");
    Console.WriteLine(password);
}

return 0;

static string? GetArg(string[] args, string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string BuildSql(string email, string name, string hash)
{
    string e = SqlEscape(email);
    string n = SqlEscape(name);
    string h = SqlEscape(hash);

    // Upsert by email (unique index) — INSERT for a brand-new admin, or
    // ON DUPLICATE KEY UPDATE to reset an existing user's password and role.
    // RoleId is resolved from the Role table by name at statement-run time,
    // so this never needs the tool to have queried the target database itself.
    return $"""
        INSERT INTO `User`
            (`Name`, `Email`, `PasswordHash`, `RoleId`, `IsActive`, `AccessFailedCount`,
             `CreatedAtUtc`, `ConcurrencyStamp`)
        SELECT '{n}', '{e}', '{h}', r.`Id`, 1, 0, UTC_TIMESTAMP(), UUID()
        FROM `Role` r
        WHERE r.`Name` = 'Administrator'
        ON DUPLICATE KEY UPDATE
            `PasswordHash` = VALUES(`PasswordHash`),
            `RoleId` = VALUES(`RoleId`),
            `IsActive` = 1,
            `UpdatedAtUtc` = UTC_TIMESTAMP(),
            `ConcurrencyStamp` = UUID();
        """;
}

static string SqlEscape(string value) => value.Replace("'", "''");

static string GenerateStrongPassword()
{
    const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // no I/O — avoids look-alikes
    const string lower = "abcdefghijkmnopqrstuvwxyz";
    const string digits = "23456789";
    const string symbols = "!@#$%^&*-_=+";
    const string all = upper + lower + digits + symbols;

    Span<char> chars = stackalloc char[20];
    chars[0] = Pick(upper);
    chars[1] = Pick(lower);
    chars[2] = Pick(digits);
    chars[3] = Pick(symbols);
    for (int i = 4; i < chars.Length; i++)
    {
        chars[i] = Pick(all);
    }

    // Shuffle so the guaranteed-category characters aren't always in the same
    // positions (Fisher-Yates using a cryptographic RNG).
    for (int i = chars.Length - 1; i > 0; i--)
    {
        int j = RandomNumberGenerator.GetInt32(i + 1);
        (chars[i], chars[j]) = (chars[j], chars[i]);
    }

    return new string(chars);

    static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
