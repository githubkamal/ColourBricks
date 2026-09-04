namespace ColourBricks.Application.Users;

public interface IUserAdminService
{
    Task<IReadOnlyList<UserListItemDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<UserListItemDto?> GetAsync(long id, CancellationToken cancellationToken);

    Task<UserListItemDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Edits profile, role, department and active state. Deactivating a user revokes
    /// every one of their refresh tokens (P1-T08 acceptance). Throws
    /// <see cref="LastAdministratorException"/> if this would remove the last active admin.
    /// </summary>
    Task<UserListItemDto?> UpdateAsync(long id, UpdateUserRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Hard-deletes a user with no audit/transaction history; otherwise throws
    /// <see cref="UserHasHistoryException"/>. Throws <see cref="LastAdministratorException"/>
    /// for the last active admin. Returns false if the user does not exist. Logs
    /// <paramref name="reason"/> to the audit trail (BRD §65).
    /// </summary>
    Task<bool> DeleteAsync(long id, string reason, CancellationToken cancellationToken);

    Task<UserListItemDto?> AssignProjectsAsync(
        long id, IReadOnlyList<long> projectIds, CancellationToken cancellationToken);

    /// <summary>Sets a new password and revokes the user's refresh tokens (force re-login).</summary>
    Task<bool> ResetPasswordAsync(long id, string newPassword, CancellationToken cancellationToken);

    Task<IReadOnlyList<RoleOptionDto>> ListRolesAsync(CancellationToken cancellationToken);
}
