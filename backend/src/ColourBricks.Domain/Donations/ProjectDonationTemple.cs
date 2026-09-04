using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Donations;

/// <summary>
/// One temple's share of a project's donation (BRD §26 — "distributed across
/// multiple temples"). The shares must total the parent
/// <see cref="ProjectDonation.DonationAmount"/>.
/// </summary>
[Auditable("temple_donations")]
public sealed class ProjectDonationTemple : BaseEntity
{
    public long ProjectDonationId { get; set; }

    /// <summary>A <c>Party</c> with the Temple role.</summary>
    public long TempleId { get; set; }

    public decimal Amount { get; set; }
}
