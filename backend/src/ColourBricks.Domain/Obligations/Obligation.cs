using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Obligations;

/// <summary>
/// Money owed (plan.md §5.1) — a vendor purchase, a subcontractor's agreed work
/// value, a temple donation allocation, custom work. Creating one records an
/// expense and increases an outstanding balance; it never moves cash. Outstanding
/// is always derived, never stored (plan.md §5.3, BRD §70 rule 53).
/// </summary>
[Auditable("project_expenses")]
public sealed class Obligation : BaseEntity
{
    public ObligationType Type { get; set; }

    public long ProjectId { get; set; }

    /// <summary>Vendor, subcontractor team or temple.</summary>
    public long? PartyId { get; set; }

    public long? DepartmentId { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>The amount owed (header total). For a purchase this equals Σ line totals.</summary>
    public decimal Amount { get; set; }

    /// <summary>Estimated cost when it differs from <see cref="Amount"/> (custom work, BRD §27).</summary>
    public decimal? EstimatedAmount { get; set; }

    /// <summary>Invoice number, work reference, etc.</summary>
    public string? Reference { get; set; }

    public string? Description { get; set; }

    /// <summary>The expense category the ledger debit posts under.</summary>
    public long CategoryId { get; set; }

    public ObligationStatus Status { get; set; } = ObligationStatus.Active;

    /// <summary>
    /// The purchase order this vendor purchase was created from, if any (client
    /// request, 2026-09-04 — a PO may span several projects; each project gets its
    /// own obligation, all carrying the same <see cref="PurchaseOrderId"/>).
    /// </summary>
    public long? PurchaseOrderId { get; set; }

    public List<ObligationLine> Lines { get; } = [];
}
