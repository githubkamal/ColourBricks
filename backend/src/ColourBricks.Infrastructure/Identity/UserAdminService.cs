using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Users;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Identity;

public sealed class UserAdminService(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider,
    IAuditService audit) : IUserAdminService
{
    public async Task<IReadOnlyList<UserListItemDto>> ListAsync(
        bool includeInactive, CancellationToken cancellationToken)
    {
        IQueryable<User> query = db.Users.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(u => u.IsActive);
        }

        List<User> users = await query.OrderBy(u => u.Name).ToListAsync(cancellationToken);
        long? adminRoleId = await AdminRoleIdAsync(cancellationToken);
        Dictionary<long, string> roleNames = await db.Roles.AsNoTracking()
            .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);

        List<long> userIds = users.Select(u => u.Id).ToList();
        ILookup<long, long> projectsByUser = (await db.UserProjectAccess.AsNoTracking()
                .Where(a => userIds.Contains(a.UserId))
                .Select(a => new { a.UserId, a.ProjectId })
                .ToListAsync(cancellationToken))
            .ToLookup(a => a.UserId, a => a.ProjectId);

        return users.Select(u => ToDto(u, adminRoleId, roleNames, projectsByUser[u.Id].ToList())).ToList();
    }

    public async Task<UserListItemDto?> GetAsync(long id, CancellationToken cancellationToken)
    {
        User? user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        long? adminRoleId = await AdminRoleIdAsync(cancellationToken);
        Dictionary<long, string> roleNames = await db.Roles.AsNoTracking()
            .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);
        List<long> projectIds = await db.UserProjectAccess.AsNoTracking()
            .Where(a => a.UserId == id).Select(a => a.ProjectId).ToListAsync(cancellationToken);

        return ToDto(user, adminRoleId, roleNames, projectIds);
    }

    public async Task<UserListItemDto> CreateAsync(
        CreateUserRequest request, CancellationToken cancellationToken)
    {
        string email = request.Email.Trim();

        if (await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken) is { } clash)
        {
            throw new UserEmailInUseException(clash.Id, email);
        }

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            Mobile = request.Mobile,
            RoleId = request.RoleId,
            DepartmentId = request.DepartmentId,
            PasswordHash = passwordHasher.Hash(request.Password),
            IsActive = true,
        };
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            User? raced = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
            throw new UserEmailInUseException(raced?.Id ?? 0, email);
        }

        if (request.ProjectIds is { Count: > 0 })
        {
            await ReplaceProjectAccessAsync(user.Id, request.ProjectIds, cancellationToken);
        }

        return (await GetAsync(user.Id, cancellationToken))!;
    }

    public async Task<UserListItemDto?> UpdateAsync(
        long id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        User? user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        bool deactivating = user.IsActive && !request.IsActive;
        if (deactivating && await IsLastActiveAdministratorAsync(id, cancellationToken))
        {
            throw new LastAdministratorException();
        }

        string email = request.Email.Trim();
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)
            && await db.Users.AnyAsync(u => u.Email == email && u.Id != id, cancellationToken))
        {
            throw new UserEmailInUseException(0, email);
        }

        db.Entry(user).Property(u => u.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;

        user.Name = request.Name.Trim();
        user.Email = email;
        user.Mobile = request.Mobile;
        user.RoleId = request.RoleId;
        user.DepartmentId = request.DepartmentId;
        user.IsActive = request.IsActive;

        if (deactivating)
        {
            RevokeActiveRefreshTokens(id);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long id, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException([new ValidationFailure("reason", "A reason is required to delete a user.")]);
        }

        User? user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return false;
        }

        if (await IsLastActiveAdministratorAsync(id, cancellationToken))
        {
            throw new LastAdministratorException();
        }

        bool hasHistory =
            await db.AuditLogs.AnyAsync(a => a.UserId == id, cancellationToken)
            || await db.LedgerEntries.AnyAsync(l => l.CreatedByUserId == id, cancellationToken);
        if (hasHistory)
        {
            throw new UserHasHistoryException();
        }

        List<UserProjectAccess> access = await db.UserProjectAccess
            .Where(a => a.UserId == id).ToListAsync(cancellationToken);
        db.UserProjectAccess.RemoveRange(access);

        List<RefreshToken> tokens = await db.RefreshTokens
            .Where(t => t.UserId == id).ToListAsync(cancellationToken);
        db.RefreshTokens.RemoveRange(tokens);

        // Companion to the interceptor's automatic "delete" row — carries the reason
        // the interceptor has no way to know (BRD §65). Recorded under the ACTING
        // admin's identity, not the deleted user's, so it survives the delete itself.
        audit.RecordAction("users", "delete", id.ToString(), reason.Trim());
        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<UserListItemDto?> AssignProjectsAsync(
        long id, IReadOnlyList<long> projectIds, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Id == id, cancellationToken) is false)
        {
            return null;
        }

        List<long> wanted = projectIds.Distinct().ToList();
        if (wanted.Count > 0)
        {
            int found = await db.Projects.CountAsync(p => wanted.Contains(p.Id), cancellationToken);
            if (found != wanted.Count)
            {
                throw new ValidationException(
                    [new ValidationFailure("projectIds", "One or more selected projects do not exist.")]);
            }
        }

        await ReplaceProjectAccessAsync(id, wanted, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<bool> ResetPasswordAsync(
        long id, string newPassword, CancellationToken cancellationToken)
    {
        User? user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return false;
        }

        user.PasswordHash = passwordHasher.Hash(newPassword);
        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        RevokeActiveRefreshTokens(id);

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<RoleOptionDto>> ListRolesAsync(CancellationToken cancellationToken) =>
        await db.Roles.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new RoleOptionDto(r.Id, r.Name))
            .ToListAsync(cancellationToken);

    private async Task ReplaceProjectAccessAsync(
        long userId, IReadOnlyCollection<long> projectIds, CancellationToken cancellationToken)
    {
        List<UserProjectAccess> existing = await db.UserProjectAccess
            .Where(a => a.UserId == userId).ToListAsync(cancellationToken);
        db.UserProjectAccess.RemoveRange(existing);

        foreach (long projectId in projectIds.Distinct())
        {
            db.UserProjectAccess.Add(new UserProjectAccess { UserId = userId, ProjectId = projectId });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private void RevokeActiveRefreshTokens(long userId)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (RefreshToken token in db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null))
        {
            token.RevokedAtUtc = now;
        }
    }

    private async Task<bool> IsLastActiveAdministratorAsync(long userId, CancellationToken cancellationToken)
    {
        long? adminRoleId = await AdminRoleIdAsync(cancellationToken);
        if (adminRoleId is null)
        {
            return false;
        }

        User? target = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (target is null || target.RoleId != adminRoleId || !target.IsActive)
        {
            return false;
        }

        int activeAdmins = await db.Users
            .CountAsync(u => u.RoleId == adminRoleId && u.IsActive, cancellationToken);
        return activeAdmins <= 1;
    }

    private Task<long?> AdminRoleIdAsync(CancellationToken cancellationToken) =>
        db.Roles.AsNoTracking()
            .Where(r => r.Name == IdentitySeeder.AdministratorRoleName)
            .Select(r => (long?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static UserListItemDto ToDto(
        User user,
        long? adminRoleId,
        IReadOnlyDictionary<long, string> roleNames,
        IReadOnlyList<long> projectIds) => new(
        user.Id,
        user.Name,
        user.Email,
        user.Mobile,
        user.RoleId,
        user.RoleId is { } rid && roleNames.TryGetValue(rid, out string? name) ? name : null,
        user.DepartmentId,
        user.IsActive,
        user.RoleId is { } r && r == adminRoleId,
        projectIds,
        user.ConcurrencyStamp);
}
