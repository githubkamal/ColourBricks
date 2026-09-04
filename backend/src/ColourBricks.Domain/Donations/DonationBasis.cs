namespace ColourBricks.Domain.Donations;

/// <summary>
/// How a project's temple donation is sized (BRD §26, §70 rule 11). Stored as
/// TINYINT (plan.md §6).
/// </summary>
public enum DonationBasis : byte
{
    Percentage = 1,
    Fixed = 2,
}
