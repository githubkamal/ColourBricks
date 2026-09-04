using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Auth;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ColourBricks.Infrastructure.Identity;

/// <summary>
/// Seeds the RBAC catalogue (permissions, the five BRD §60 roles, their grants) and
/// the first Administrator account (plan.md P0-T04, P0-T05). Idempotent and resilient
/// to a not-yet-migrated database on first run.
/// </summary>
public sealed class IdentitySeeder(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<AuthOptions> authOptions,
    ILogger<IdentitySeeder> logger)
{
    public const string AdministratorRoleName = "Administrator";

    private readonly AuthOptions.SeedAdministrator _seed = authOptions.Value.Seed;

    /// <summary>
    /// Seeds the RBAC catalogue and the Administrator account. Idempotent. Throws if
    /// the database is not reachable — the caller (Program) swallows that on startup;
    /// tests want it to fail loudly.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRbacAsync(cancellationToken);
        await SeedAdministratorAsync(cancellationToken);
    }

    private async Task SeedRbacAsync(CancellationToken cancellationToken)
    {
        PermissionMatrix matrix = PermissionMatrix.Load();

        // 1. Permission catalogue.
        HashSet<string> existingKeys = (await db.Permissions
                .Select(p => p.Key)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        foreach (string key in matrix.AllPermissionKeys.Where(k => !existingKeys.Contains(k)))
        {
            string[] parts = key.Split('.', 2);
            db.Permissions.Add(new Permission { Key = key, Module = parts[0], Action = parts[1] });
        }

        await db.SaveChangesAsync(cancellationToken);

        Dictionary<string, long> permissionIds = await db.Permissions
            .ToDictionaryAsync(p => p.Key, p => p.Id, cancellationToken);

        // 2. Roles.
        Dictionary<string, Role> rolesByName = await db.Roles
            .ToDictionaryAsync(r => r.Name, cancellationToken);

        foreach (PermissionMatrix.RoleDefinition def in matrix.Roles)
        {
            if (!rolesByName.TryGetValue(def.Name, out Role? role))
            {
                role = new Role { Name = def.Name };
                db.Roles.Add(role);
                rolesByName[def.Name] = role;
            }

            role.Description = def.Description;
            role.IsSystem = def.IsSystem;
        }

        await db.SaveChangesAsync(cancellationToken);

        // 3. Role → permission grants.
        HashSet<(long RoleId, long PermissionId)> existingGrants = (await db.RolePermissions
                .Select(rp => new { rp.RoleId, rp.PermissionId })
                .ToListAsync(cancellationToken))
            .Select(x => (x.RoleId, x.PermissionId))
            .ToHashSet();

        foreach (PermissionMatrix.RoleDefinition def in matrix.Roles)
        {
            long roleId = rolesByName[def.Name].Id;
            foreach (string key in matrix.KeysFor(def))
            {
                long permissionId = permissionIds[key];
                if (existingGrants.Add((roleId, permissionId)))
                {
                    db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAdministratorAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_seed.Email) || string.IsNullOrWhiteSpace(_seed.Password))
        {
            logger.LogInformation("No Auth:Seed administrator configured; skipping user seed.");
            return;
        }

        long? adminRoleId = _seed.RoleId
            ?? await db.Roles
                .Where(r => r.Name == AdministratorRoleName)
                .Select(r => (long?)r.Id)
                .FirstOrDefaultAsync(cancellationToken);

        User? existing = await db.Users
            .FirstOrDefaultAsync(u => u.Email == _seed.Email, cancellationToken);

        if (existing is not null)
        {
            // Backfill the role onto an account seeded before P0-T05 existed.
            if (existing.RoleId is null && adminRoleId is not null)
            {
                existing.RoleId = adminRoleId;
                await db.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        db.Users.Add(new User
        {
            Name = string.IsNullOrWhiteSpace(_seed.Name) ? "Administrator" : _seed.Name,
            Email = _seed.Email,
            PasswordHash = passwordHasher.Hash(_seed.Password),
            RoleId = adminRoleId,
            IsActive = true,
        });

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded administrator account {Email}.", _seed.Email);
    }
}
