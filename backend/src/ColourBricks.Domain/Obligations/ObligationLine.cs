using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Obligations;

/// <summary>
/// One purchase line (BRD §16). The item's name, unit, rate and tax are snapshotted
/// at entry (plan.md §5.2 — a later change to the item master never rewrites this).
/// </summary>
public sealed class ObligationLine : BaseEntity
{
    public long ObligationId { get; set; }

    public long? ItemId { get; set; }

    public required string ItemName { get; set; }

    public decimal Quantity { get; set; }

    public required string Unit { get; set; }

    public decimal Rate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }
}
