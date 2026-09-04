using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Allocations;

/// <summary>
/// Links a <c>Settlement</c> to a project (and optionally a specific obligation) for
/// some portion of its amount (plan.md §5.2). A consolidated vendor payment produces
/// several of these — one per project it settles. The sum of a settlement's active
/// allocations equals the amount that was applied.
/// </summary>
[Auditable("vendor_payment_allocation")]
public sealed class Allocation : BaseEntity
{
    /// <summary>
    /// The settlement (cash movement) this slice belongs to. Null for a non-cash
    /// event: applying an existing vendor advance against a later purchase (P3-T05).
    /// </summary>
    public long? SettlementId { get; set; }

    /// <summary>The obligation this slice pays down. Null = "against the project, no specific invoice" — also the shape of a vendor advance (P3-T05).</summary>
    public long? ObligationId { get; set; }

    /// <summary>The project this slice applies to. Null for a vendor advance, which is not project-scoped (P3-T05).</summary>
    public long? ProjectId { get; set; }

    public long PartyId { get; set; }

    /// <summary>Positive for an allocation or an advance; negative when an advance is consumed (P3-T05).</summary>
    public decimal Amount { get; set; }

    /// <summary>Fifo | Manual | Auto | Advance | AdvanceApplied (plan.md §5.2, P3-T05).</summary>
    public string Method { get; set; } = "Fifo";
}
