using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Parties;

/// <summary>
/// One site a subcontractor team is contracted for, with its contract value (client
/// request, 2026-11-21 — a team can work several sites at once). Belongs to a
/// <see cref="Party"/> playing the Subcontractor role; the project link is optional
/// so a site not yet set up as a project can still be recorded.
/// </summary>
[Auditable("vendors")]
public sealed class TeamSite : BaseEntity
{
    public long PartyId { get; set; }

    public long? ProjectId { get; set; }

    public string? SiteLocation { get; set; }

    public decimal ContractValue { get; set; }
}
