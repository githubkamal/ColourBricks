namespace ColourBricks.Application.Roles;

public sealed record RoleSummaryDto(
    long Id, string Name, string? Description, bool IsSystem, bool IsActive, int PermissionCount);

public sealed record RoleDetailDto(
    long Id,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsActive,
    IReadOnlyList<string> PermissionKeys,
    string ConcurrencyStamp);

public sealed record CatalogueModuleDto(string Key, string Name);

public sealed record PermissionCatalogueDto(
    IReadOnlyList<CatalogueModuleDto> Modules, IReadOnlyList<string> Actions);

public sealed record CreateRoleRequest(string Name, string? Description = null);

public sealed record UpdateRoleRequest(
    string Name, string? Description, bool IsActive, string ConcurrencyStamp);

public sealed record SetRolePermissionsRequest(IReadOnlyList<string> PermissionKeys);

public sealed class RoleNameInUseException(long existingId, string name)
    : Exception($"A role named '{name}' already exists.")
{
    public long ExistingId { get; } = existingId;
}

/// <summary>The Administrator role would drop below its minimum permission set. Maps to 400.</summary>
public sealed class AdministratorCorePermissionsException(IReadOnlyList<string> missing)
    : Exception("The Administrator role cannot lose its core permissions: " + string.Join(", ", missing))
{
    public IReadOnlyList<string> Missing { get; } = missing;
}

/// <summary>A system role cannot be deleted or deactivated. Maps to 400.</summary>
public sealed class SystemRoleImmutableException(string message) : Exception(message);
