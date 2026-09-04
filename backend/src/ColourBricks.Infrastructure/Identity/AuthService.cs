using System.Security.Cryptography;
using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Auth;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ColourBricks.Infrastructure.Identity;

/// <summary>
/// Password login + rotating refresh tokens with family-wide revocation on reuse
/// (plan.md §9).
/// </summary>
public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    JwtTokenGenerator tokenGenerator,
    IOptions<AuthOptions> authOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly AuthOptions _options = authOptions.Value;

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        User? user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            return AuthResult.Fail(AuthOutcome.InvalidCredentials);
        }

        if (user.LockoutEndUtc is { } lockoutEnd && lockoutEnd > now)
        {
            return AuthResult.Fail(AuthOutcome.LockedOut);
        }

        if (passwordHasher.Verify(user.PasswordHash, password) == PasswordVerification.Failed)
        {
            return await RegisterFailedAttempt(user, now, cancellationToken);
        }

        if (!user.IsActive)
        {
            return AuthResult.Fail(AuthOutcome.InactiveUser);
        }

        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;

        PermissionSet permissions = await LoadPermissionsAsync(user, cancellationToken);
        AuthTokens tokens = Issue(user, Guid.NewGuid(), now, permissions);
        await db.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(tokens, ToDto(user, permissions));
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        string hash = HashToken(refreshToken);

        RefreshToken? stored = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null || stored.RevokedAtUtc is not null)
        {
            return AuthResult.Fail(AuthOutcome.InvalidRefreshToken);
        }

        if (stored.ConsumedAtUtc is not null)
        {
            // A consumed token is being replayed — the family is compromised.
            await RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "Refresh-token reuse detected for user {UserId}; family {FamilyId} revoked.",
                stored.UserId, stored.FamilyId);
            return AuthResult.Fail(AuthOutcome.InvalidRefreshToken);
        }

        if (stored.ExpiresAtUtc <= now)
        {
            return AuthResult.Fail(AuthOutcome.InvalidRefreshToken);
        }

        User? user = await db.Users.FirstOrDefaultAsync(u => u.Id == stored.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            stored.RevokedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            return AuthResult.Fail(user is null ? AuthOutcome.InvalidRefreshToken : AuthOutcome.InactiveUser);
        }

        stored.ConsumedAtUtc = now;
        PermissionSet permissions = await LoadPermissionsAsync(user, cancellationToken);
        AuthTokens tokens = Issue(user, stored.FamilyId, now, permissions);
        stored.ReplacedByTokenHash = HashToken(tokens.RefreshToken);
        await db.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(tokens, ToDto(user, permissions));
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        string hash = HashToken(refreshToken);
        RefreshToken? stored = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is { RevokedAtUtc: null })
        {
            stored.RevokedAtUtc = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(long userId, CancellationToken cancellationToken)
    {
        User? user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);

        if (user is null)
        {
            return null;
        }

        PermissionSet permissions = await LoadPermissionsAsync(user, cancellationToken);
        return ToDto(user, permissions);
    }

    private async Task<AuthResult> RegisterFailedAttempt(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        user.AccessFailedCount++;

        bool nowLocked = user.AccessFailedCount >= _options.MaxFailedAttempts;
        if (nowLocked)
        {
            user.LockoutEndUtc = now.AddMinutes(_options.LockoutMinutes);
            user.AccessFailedCount = 0;
        }

        await db.SaveChangesAsync(cancellationToken);
        return AuthResult.Fail(nowLocked ? AuthOutcome.LockedOut : AuthOutcome.InvalidCredentials);
    }

    private AuthTokens Issue(User user, Guid familyId, DateTimeOffset now, PermissionSet permissions)
    {
        (string accessToken, DateTimeOffset accessExpiry) =
            tokenGenerator.CreateAccessToken(user, permissions.Keys, permissions.GrantsEveryPermission);

        string rawRefresh = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        DateTimeOffset refreshExpiry = now.AddDays(_options.RefreshTokenDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawRefresh),
            FamilyId = familyId,
            ExpiresAtUtc = refreshExpiry,
        });

        return new AuthTokens(accessToken, accessExpiry, rawRefresh, refreshExpiry);
    }

    private async Task<PermissionSet> LoadPermissionsAsync(User user, CancellationToken cancellationToken)
    {
        if (user.RoleId is not { } roleId)
        {
            return new PermissionSet([], false);
        }

        string[] keys = await db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission!.Key)
            .ToArrayAsync(cancellationToken);

        int catalogueSize = await db.Permissions.CountAsync(cancellationToken);
        return new PermissionSet(keys, keys.Length > 0 && keys.Length == catalogueSize);
    }

    private readonly record struct PermissionSet(string[] Keys, bool GrantsEveryPermission)
    {
        /// <summary>What the SPA sees: <c>["*"]</c> for full access, else the explicit list.</summary>
        public IReadOnlyList<string> ForClient => GrantsEveryPermission ? ["*"] : Keys;
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<RefreshToken> family = await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (RefreshToken token in family)
        {
            token.RevokedAtUtc = now;
        }
    }

    private static string HashToken(string token)
    {
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static CurrentUserDto ToDto(User user, PermissionSet permissions) =>
        new(user.Id, user.Name, user.Email, user.Mobile, user.RoleId, user.DepartmentId, permissions.ForClient);
}
