using System.Text.Json;
using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Roles;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Identity;

public sealed class RoleAdminService(AppDbContext db, IAuditService audit) : IRoleAdminService
{
    /// <summary>
    /// The permissions the Administrator role can never lose — enough to undo any
    /// mistake made in the matrix editor (P1-T09 guard).
    /// </summary>
    private static readonly string[] AdministratorCoreKeys =
    [
        "roles.view", "roles.add", "roles.edit", "roles.delete",
        "permissions.view", "permissions.edit",
        "users.view", "users.add", "users.edit",
        "admin_configuration.view", "admin_configuration.edit",
        "audit_trail.view",
    ];

    public async Task<IReadOnlyList<RoleSummaryDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Roles.AsNoTracking()
            .Select(r => new
            {
                r.Id, r.Name, r.Description, r.IsSystem, r.IsActive,
                Count = db.RolePermissions.Count(rp => rp.RoleId == r.Id),
            })
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new RoleSummaryDto(r.Id, r.Name, r.Description, r.IsSystem, r.IsActive, r.Count))
            .ToList();
    }

    public async Task<RoleDetailDto?> GetAsync(long id, CancellationToken cancellationToken)
    {
        Role? role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role is null)
        {
            return null;
        }

        List<string> keys = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == id)
            .Select(rp => rp.Permission!.Key)
            .OrderBy(k => k)
            .ToListAsync(cancellationToken);

        return new RoleDetailDto(
            role.Id, role.Name, role.Description, role.IsSystem, role.IsActive, keys, role.ConcurrencyStamp);
    }

    public Task<PermissionCatalogueDto> GetCatalogueAsync(CancellationToken cancellationToken)
    {
        PermissionMatrix matrix = PermissionMatrix.Load();
        var dto = new PermissionCatalogueDto(
            matrix.Modules.Select(m => new CatalogueModuleDto(m.Key, m.Name)).ToList(),
            matrix.Actions.ToList());
        return Task.FromResult(dto);
    }

    public async Task<RoleDetailDto> CreateAsync(
        CreateRoleRequest request, CancellationToken cancellationToken)
    {
        string name = request.Name.Trim();
        if (await db.Roles.FirstOrDefaultAsync(r => r.Name == name, cancellationToken) is { } clash)
        {
            throw new RoleNameInUseException(clash.Id, name);
        }

        var role = new Role { Name = name, Description = request.Description, IsActive = true, IsSystem = false };
        db.Roles.Add(role);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            Role? raced = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == name, cancellationToken);
            throw new RoleNameInUseException(raced?.Id ?? 0, name);
        }

        return (await GetAsync(role.Id, cancellationToken))!;
    }

    public async Task<RoleDetailDto?> UpdateAsync(
        long id, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        Role? role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role is null)
        {
            return null;
        }

        if (role.Name == IdentitySeeder.AdministratorRoleName && !request.IsActive)
        {
            throw new SystemRoleImmutableException("The Administrator role cannot be deactivated.");
        }

        string name = request.Name.Trim();
        if (!string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase)
            && await db.Roles.AnyAsync(r => r.Name == name && r.Id != id, cancellationToken))
        {
            throw new RoleNameInUseException(0, name);
        }

        db.Entry(role).Property(r => r.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;
        role.Name = name;
        role.Description = request.Description;
        role.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<RoleDetailDto?> SetPermissionsAsync(
        long id, SetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        Role? role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role is null)
        {
            return null;
        }

        HashSet<string> requested = request.PermissionKeys
            .Select(k => k.Trim())
            .Where(k => k.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        Dictionary<string, long> catalogue = await db.Permissions.AsNoTracking()
            .ToDictionaryAsync(p => p.Key, p => p.Id, cancellationToken);

        string[] unknown = requested.Where(k => !catalogue.ContainsKey(k)).ToArray();
        if (unknown.Length > 0)
        {
            throw new ValidationException(
                [new ValidationFailure("permissionKeys", $"Unknown permission keys: {string.Join(", ", unknown)}")]);
        }

        if (role.Name == IdentitySeeder.AdministratorRoleName)
        {
            string[] missing = AdministratorCoreKeys.Where(k => !requested.Contains(k)).ToArray();
            if (missing.Length > 0)
            {
                throw new AdministratorCorePermissionsException(missing);
            }
        }

        List<RolePermission> current = await db.RolePermissions
            .Where(rp => rp.RoleId == id)
            .ToListAsync(cancellationToken);

        HashSet<long> currentIds = current.Select(rp => rp.PermissionId).ToHashSet();
        HashSet<long> desiredIds = requested.Select(k => catalogue[k]).ToHashSet();

        List<RolePermission> toRemove = current.Where(rp => !desiredIds.Contains(rp.PermissionId)).ToList();
        List<long> toAdd = desiredIds.Where(pid => !currentIds.Contains(pid)).ToList();

        db.RolePermissions.RemoveRange(toRemove);
        foreach (long permissionId in toAdd)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = id, PermissionId = permissionId });
        }

        var reverse = catalogue.ToDictionary(kv => kv.Value, kv => kv.Key);
        string details = JsonSerializer.Serialize(new
        {
            added = toAdd.Select(pid => reverse[pid]).OrderBy(k => k).ToArray(),
            removed = toRemove.Select(rp => reverse[rp.PermissionId]).OrderBy(k => k).ToArray(),
        });
        audit.RecordAction("roles", "permissions_updated", id.ToString(), details);

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long id, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException([new ValidationFailure("reason", "A reason is required to delete a role.")]);
        }

        Role? role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role is null)
        {
            return false;
        }

        if (role.IsSystem)
        {
            throw new SystemRoleImmutableException("A system role cannot be deleted.");
        }

        if (await db.Users.AnyAsync(u => u.RoleId == id, cancellationToken))
        {
            throw new SystemRoleImmutableException("This role is assigned to users. Reassign them first.");
        }

        // The interceptor logs the automatic "delete" row (old field values); this
        // companion row carries the reason the interceptor has no way to know (BRD §65).
        audit.RecordAction("roles", "delete", id.ToString(), reason.Trim());
        db.Roles.Remove(role);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
