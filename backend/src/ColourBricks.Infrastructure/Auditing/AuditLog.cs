using ColourBricks.Domain.Common;

namespace ColourBricks.Infrastructure.Auditing;

/// <summary>
/// One audit-trail row (BRD §65). Append-only — never edited, never deleted; the
/// SaveChanges guard enforces this.
/// </summary>
public sealed class AuditLog : BaseEntity
{
    /// <summary>The acting user, or null for system/seed actions.</summary>
    public long? UserId { get; set; }

    public DateTimeOffset TimestampUtc { get; set; }

    public required string Module { get; set; }

    /// <summary>create | update | delete, or a domain verb (reconcile, unreconcile, …).</summary>
    public required string Action { get; set; }

    public string? EntityType { get; set; }

    public required string RecordId { get; set; }

    /// <summary>JSON of the changed properties' prior values (text — plan.md §3.1).</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON of the changed properties' new values.</summary>
    public string? NewValues { get; set; }

    /// <summary>Free-form context for non-CRUD actions recorded via <c>IAuditService</c>.</summary>
    public string? Details { get; set; }

    // Reconciliation-specific fields (BRD §65 "New"). Populated from P4 onward.
    public string? ReconciliationStatus { get; set; }
    public long? ReconciliationUserId { get; set; }
    public DateTimeOffset? ReconciliationDate { get; set; }
    public string? ReversalHistory { get; set; }
}
