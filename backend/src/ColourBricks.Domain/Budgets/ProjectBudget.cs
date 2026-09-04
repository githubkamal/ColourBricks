using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Budgets;

/// <summary>
/// One versioned budget for a project (BRD §40, §41). Revisions are never
/// overwritten — a change creates a new revision with a higher
/// <see cref="RevisionNumber"/>; the current budget is the highest one.
/// </summary>
public sealed class ProjectBudgetRevision : BaseEntity
{
    public long ProjectId { get; set; }

    public int RevisionNumber { get; set; }

    /// <summary>Percent of a line's budget at which it is flagged "Approaching" (BRD §41, default 90).</summary>
    public decimal ApproachingThresholdPercent { get; set; } = 90m;

    public string? Note { get; set; }

    public ICollection<ProjectBudgetLine> Lines { get; set; } = [];
}

/// <summary>A single category's budgeted amount within a <see cref="ProjectBudgetRevision"/>.</summary>
public sealed class ProjectBudgetLine : BaseEntity
{
    public long RevisionId { get; set; }

    public long CategoryId { get; set; }

    public decimal Amount { get; set; }
}
