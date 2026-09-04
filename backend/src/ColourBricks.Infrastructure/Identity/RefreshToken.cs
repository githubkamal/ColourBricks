using ColourBricks.Domain.Common;

namespace ColourBricks.Infrastructure.Identity;

/// <summary>
/// One issued refresh token, stored only as a SHA-256 hash. Tokens rotate on every
/// use; a whole <see cref="FamilyId"/> is revoked when a consumed token is replayed
/// (reuse detection — plan.md §9).
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    public long UserId { get; set; }

    /// <summary>Lowercase hex SHA-256 of the raw token. Unique.</summary>
    public required string TokenHash { get; set; }

    /// <summary>Shared by every token in one login's rotation lineage.</summary>
    public Guid FamilyId { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>Set when the token is rotated (exchanged for a successor).</summary>
    public DateTimeOffset? ConsumedAtUtc { get; set; }

    /// <summary>Set when the token is invalidated (logout, or family revocation).</summary>
    public DateTimeOffset? RevokedAtUtc { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive(DateTimeOffset now) =>
        RevokedAtUtc is null && ConsumedAtUtc is null && ExpiresAtUtc > now;
}
