using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Items;

/// <summary>
/// Groups items for reporting (BRD §14: "Category", §70 rule 16). A short,
/// admin-extensible list seeded from the BRD §14 examples.
/// </summary>
[Auditable("materials")]
public sealed class ItemCategory : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>Lowercase, punctuation-stripped, whitespace-collapsed. Unique (plan.md §6).</summary>
    public required string NormalisedName { get; set; }

    public bool IsActive { get; set; } = true;
}
