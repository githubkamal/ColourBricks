using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Notifications;

/// <summary>The ten BRD §66 notification triggers (the last two are the P4 additions).</summary>
public static class NotificationTrigger
{
    public const string BudgetExceeded = "budget_exceeded";
    public const string BudgetApproaching = "budget_approaching";
    public const string VendorPaymentOverdue = "vendor_payment_overdue";
    public const string SubcontractorPaymentOverdue = "subcontractor_payment_overdue";
    public const string EmiDue = "emi_due";
    public const string EmiOverdue = "emi_overdue";
    public const string ProfitabilityBelowThreshold = "profitability_below_threshold";
    public const string VendorOutstandingLimit = "vendor_outstanding_limit";
    public const string PendingReconciliation = "pending_reconciliation";
    public const string BankAllocationMismatch = "bank_allocation_mismatch";

    public static readonly IReadOnlyList<string> All =
    [
        BudgetExceeded, BudgetApproaching, VendorPaymentOverdue, SubcontractorPaymentOverdue,
        EmiDue, EmiOverdue, ProfitabilityBelowThreshold, VendorOutstandingLimit,
        PendingReconciliation, BankAllocationMismatch,
    ];

    public static bool IsValid(string? trigger) => trigger is not null && All.Contains(trigger);
}

/// <summary>How a trigger reaches a role (BRD §66). TINYINT.</summary>
public enum NotificationChannel : byte
{
    Off = 0,
    Dashboard = 1,
    Email = 2,
    Both = 3,
}

/// <summary>
/// A raised alert (BRD §66). One row per <see cref="DedupeKey"/> — the evaluator
/// re-runs safely and never re-raises the same situation. Email delivery state is
/// tracked here so a failed send is retried, not lost, and never blocks the job.
/// </summary>
[Auditable("notifications")]
public sealed class Notification : BaseEntity
{
    public string Trigger { get; set; } = "";

    /// <summary>Stable identity of the situation — e.g. <c>budget_exceeded:12:3</c>. Unique.</summary>
    public string DedupeKey { get; set; } = "";

    public string Title { get; set; } = "";

    public string Body { get; set; } = "";

    public string Severity { get; set; } = "Warning";

    public long? ProjectId { get; set; }

    public string? EntityType { get; set; }

    public long? EntityId { get; set; }

    public int EmailAttempts { get; set; }

    public DateTimeOffset? EmailSentAtUtc { get; set; }

    public string? EmailError { get; set; }
}

/// <summary>Per-user read state for one notification.</summary>
public sealed class NotificationRead : BaseEntity
{
    public long NotificationId { get; set; }

    public long UserId { get; set; }

    public DateTimeOffset ReadAtUtc { get; set; }
}

/// <summary>A user has muted a whole trigger for themselves (BRD §66).</summary>
public sealed class NotificationMute : BaseEntity
{
    public long UserId { get; set; }

    public string Trigger { get; set; } = "";
}

/// <summary>The channel a trigger uses for a role (BRD §66 — configurable per trigger per role).</summary>
public sealed class NotificationChannelConfig : BaseEntity
{
    public long RoleId { get; set; }

    public string Trigger { get; set; } = "";

    public NotificationChannel Channel { get; set; } = NotificationChannel.Dashboard;
}
