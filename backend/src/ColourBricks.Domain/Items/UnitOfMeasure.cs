using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Items;

/// <summary>
/// A unit an item is bought in — Bag, Load, Nos, Kg, Ton, Sqft, Rft, Litre
/// (BRD §14, §16). A small configurable list; an <see cref="Item"/> and every
/// purchase line copy the <see cref="Code"/> as a plain string, so renaming or
/// deactivating a unit never rewrites history.
/// </summary>
[Auditable("materials")]
public sealed class UnitOfMeasure : BaseEntity
{
    public required string Code { get; set; }

    /// <summary>Lowercase, punctuation-stripped. Unique — used for case-insensitive matching.</summary>
    public required string NormalisedCode { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
