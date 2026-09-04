namespace ColourBricks.Application.Users;

public sealed record UserListItemDto(
    long Id,
    string Name,
    string Email,
    string? Mobile,
    long? RoleId,
    string? RoleName,
    long? DepartmentId,
    bool IsActive,
    bool IsAdministrator,
    IReadOnlyList<long> AssignedProjectIds,
    string ConcurrencyStamp);

public sealed record CreateUserRequest(
    string Name,
    string Email,
    string Password,
    string? Mobile = null,
    long? RoleId = null,
    long? DepartmentId = null,
    IReadOnlyList<long>? ProjectIds = null);

public sealed record UpdateUserRequest(
    string Name,
    string Email,
    bool IsActive,
    string ConcurrencyStamp,
    string? Mobile = null,
    long? RoleId = null,
    long? DepartmentId = null);

public sealed record AssignProjectsRequest(IReadOnlyList<long> ProjectIds);

public sealed record ResetPasswordRequest(string NewPassword);

public sealed record RoleOptionDto(long Id, string Name);

public sealed class UserEmailInUseException(long existingId, string email)
    : Exception($"A user with the email '{email}' already exists.")
{
    public long ExistingId { get; } = existingId;
}

/// <summary>
/// The operation would leave the system with no active Administrator
/// (P1-T08 acceptance). Maps to 400.
/// </summary>
public sealed class LastAdministratorException()
    : Exception("The last active administrator cannot be deactivated or removed.");

/// <summary>
/// The user has audit or transaction history and can only be deactivated, not
/// hard-deleted (BRD §59, plan.md §5.6). Maps to 400.
/// </summary>
public sealed class UserHasHistoryException()
    : Exception("This user has activity history and cannot be deleted. Deactivate them instead.");
