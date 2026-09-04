namespace ColourBricks.Domain.Common;

/// <summary>
/// Base type for every persisted entity. Carries the audit and concurrency
/// columns required on every table by plan.md §6.
/// </summary>
/// <remarks>
/// This is a plain POCO — the Domain layer references nothing outside the BCL
/// (plan.md §4). Column types, precision and the concurrency token are configured
/// in <c>AppDbContext</c>; the values are stamped by the SaveChanges interceptor.
/// </remarks>
public abstract class BaseEntity
{
    /// <summary>BIGINT AUTO_INCREMENT primary key (plan.md §6).</summary>
    public long Id { get; set; }

    /// <summary>UTC instant the row was inserted. Stamped by the interceptor.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>User who inserted the row. Null for system/seed operations.</summary>
    public long? CreatedByUserId { get; set; }

    /// <summary>UTC instant of the last update. Null until the row is first modified.</summary>
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    /// <summary>User who last updated the row. Null until the row is first modified.</summary>
    public long? UpdatedByUserId { get; set; }

    /// <summary>
    /// Optimistic-concurrency token (CHAR(36) GUID). MySQL/MariaDB has no
    /// <c>rowversion</c>, so this is configured with <c>.IsConcurrencyToken()</c>
    /// and rotated by the interceptor on every insert and update (plan.md §6).
    /// </summary>
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();
}
