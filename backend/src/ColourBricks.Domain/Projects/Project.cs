using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Projects;

/// <summary>
/// The Project master (BRD §4, plan.md §5.2). Completed and cancelled projects stay
/// fully readable — a status change never hides a project (BRD §70 rule 31).
/// </summary>
[Auditable("projects")]
public sealed class Project : BaseEntity
{
    /// <summary>Unique human code, <c>CB-{yyyy}-{seq}</c> by default (BRD §70 rule 1).</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    /// <summary>The client, a <c>Party</c> of type Client. FK added in P1-T02.</summary>
    public long? ClientId { get; set; }

    public string? SiteAddress { get; set; }

    public string? ContactDetails { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? ExpectedEndDate { get; set; }

    public DateOnly? ActualEndDate { get; set; }

    public decimal ContractValue { get; set; }

    /// <summary>Estimated / approved cost — required (BRD §70 rule 2).</summary>
    public decimal EstimatedCost { get; set; }

    public decimal? ExpectedProfit { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Ongoing;

    /// <summary>The project manager, a <c>User</c>.</summary>
    public long? ManagerId { get; set; }

    public string? Notes { get; set; }

    /// <summary>Soft-delete flag for masters (plan.md §5.6). Not a lifecycle status.</summary>
    public bool IsActive { get; set; } = true;
}
