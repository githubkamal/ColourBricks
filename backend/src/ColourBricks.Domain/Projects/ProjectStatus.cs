namespace ColourBricks.Domain.Projects;

/// <summary>BRD §4 project lifecycle. Stored as TINYINT (plan.md §6).</summary>
public enum ProjectStatus : byte
{
    Ongoing = 1,
    Completed = 2,
    OnHold = 3,
    Cancelled = 4,
}
