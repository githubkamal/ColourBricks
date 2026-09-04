using ColourBricks.Domain.Common;

namespace ColourBricks.Infrastructure.Identity;

/// <summary>
/// An application user (BRD §59). <see cref="DepartmentId"/> has no FK yet — the
/// <c>Department</c> entity lands in P1-T04.
/// </summary>
[Auditable("users")]
public sealed class User : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>Login identifier. Unique (case-insensitive via the table collation).</summary>
    public required string Email { get; set; }

    public string? Mobile { get; set; }

    public required string PasswordHash { get; set; }

    public long? RoleId { get; set; }

    public long? DepartmentId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Consecutive failed login attempts since the last success. Reset on success.</summary>
    public int AccessFailedCount { get; set; }

    /// <summary>When set and in the future, login is refused regardless of the password.</summary>
    public DateTimeOffset? LockoutEndUtc { get; set; }
}
