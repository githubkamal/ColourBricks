using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Departments;

/// <summary>
/// A trade/discipline that groups subcontractor teams — Mesthri/Building
/// Construction, Interior, Plumbing, Electrical, plus any added through
/// administration (BRD §8, §70 rules 8, 9). Deactivated rather than deleted so
/// historical work entries keep resolving (plan.md §5.6).
/// </summary>
[Auditable("labour")]
public sealed class Department : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>Lowercase, punctuation-stripped, whitespace-collapsed. Unique (plan.md §6).</summary>
    public required string NormalisedName { get; set; }

    public bool IsActive { get; set; } = true;
}
