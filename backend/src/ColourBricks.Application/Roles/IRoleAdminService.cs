namespace ColourBricks.Application.Roles;

public interface IRoleAdminService
{
    Task<IReadOnlyList<RoleSummaryDto>> ListAsync(CancellationToken cancellationToken);

    Task<RoleDetailDto?> GetAsync(long id, CancellationToken cancellationToken);

    /// <summary>The modules × actions grid the matrix editor renders (BRD §62).</summary>
    Task<PermissionCatalogueDto> GetCatalogueAsync(CancellationToken cancellationToken);

    Task<RoleDetailDto> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken);

    Task<RoleDetailDto?> UpdateAsync(long id, UpdateRoleRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a role's grants with the given keys, writes a <c>permissions_updated</c>
    /// audit row, and (for the Administrator role) enforces the core-permission floor.
    /// Affected users pick the change up on their next token refresh.
    /// </summary>
    Task<RoleDetailDto?> SetPermissionsAsync(
        long id, SetRolePermissionsRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes the role and logs <paramref name="reason"/> to the audit trail (BRD §65).</summary>
    Task<bool> DeleteAsync(long id, string reason, CancellationToken cancellationToken);
}
