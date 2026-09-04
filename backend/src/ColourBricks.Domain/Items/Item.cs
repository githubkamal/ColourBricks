using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Items;

/// <summary>
/// A material or item in the central Item Master (BRD §14). Created once — during
/// setup or on first use in a purchase (BRD §15, §70 rule 15) — and reused by
/// search thereafter (rule 16).
/// </summary>
/// <remarks>
/// <see cref="DefaultRate"/>, <see cref="TaxRate"/> and <see cref="Unit"/> are
/// <em>defaults</em> only. A purchase line copies them at the moment it is entered
/// (plan.md §5.2 — ObligationLine carries its own Unit/Rate/Tax), so editing the
/// master here never changes a historical line (P1-T03 acceptance).
/// </remarks>
[Auditable("materials")]
public sealed class Item : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>Lowercase, punctuation-stripped, whitespace-collapsed. Unique (plan.md §6).</summary>
    public required string NormalisedName { get; set; }

    public long? CategoryId { get; set; }

    /// <summary>The <see cref="UnitOfMeasure.Code"/> this item is usually bought in.</summary>
    public required string Unit { get; set; }

    public decimal DefaultRate { get; set; }

    /// <summary>Tax/GST percentage, e.g. <c>18.00</c> (BRD §14 "Tax/GST").</summary>
    public decimal TaxRate { get; set; }

    public bool IsActive { get; set; } = true;
}
