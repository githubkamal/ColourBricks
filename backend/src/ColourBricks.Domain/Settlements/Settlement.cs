using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Settlements;

/// <summary>
/// Money moving (plan.md §5.2). A settlement never creates an expense — it settles
/// an obligation and changes a cash/bank balance. An income settlement
/// (<see cref="Direction"/> == In) is recorded against exactly one project
/// (BRD §6, §70 rule 46) — <see cref="ProjectId"/> is non-null with a single FK.
/// </summary>
[Auditable("project_income")]
public sealed class Settlement : BaseEntity
{
    public SettlementDirection Direction { get; set; }

    /// <summary>
    /// The project this settlement belongs to. An income settlement always has exactly
    /// one (BRD §70 rule 46, enforced in the request validator); a consolidated
    /// multi-project vendor payment leaves this null and attributes the amount through
    /// <c>Allocation</c> rows instead.
    /// </summary>
    public long? ProjectId { get; set; }

    /// <summary>Counterparty, if any (null for pure project income).</summary>
    public long? PartyId { get; set; }

    /// <summary>The obligation this settlement pays against, if any (e.g. an inline vendor part-payment).</summary>
    public long? ObligationId { get; set; }

    /// <summary>Set for income settlements (BRD §6).</summary>
    public IncomeType? IncomeType { get; set; }

    /// <summary>Set for labour/subcontractor payments (BRD §10).</summary>
    public PaymentFrequency? Frequency { get; set; }

    public DateOnly Date { get; set; }

    public decimal Amount { get; set; }

    public long PaymentModeId { get; set; }

    public long? AccountId { get; set; }

    public string? ReferenceNo { get; set; }

    public string? Description { get; set; }

    public SettlementStatus Status { get; set; } = SettlementStatus.Active;
}
