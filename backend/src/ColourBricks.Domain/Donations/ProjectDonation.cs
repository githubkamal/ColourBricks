using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Donations;

/// <summary>
/// The temple-donation reservation for one project (BRD §26). One row per project.
/// The <see cref="DonationAmount"/> is computed from the basis and the project's
/// contract value at the time it was set or last recomputed; the split across
/// temples is held in <see cref="ProjectDonationTemple"/> rows.
/// </summary>
[Auditable("temple_donations")]
public sealed class ProjectDonation : BaseEntity
{
    public long ProjectId { get; set; }

    public DonationBasis Basis { get; set; }

    /// <summary>Percent of contract value when <see cref="Basis"/> is Percentage, e.g. <c>2.00</c>.</summary>
    public decimal? Percentage { get; set; }

    /// <summary>The flat amount when <see cref="Basis"/> is Fixed.</summary>
    public decimal? FixedAmount { get; set; }

    /// <summary>The project contract value the current <see cref="DonationAmount"/> was computed against.</summary>
    public decimal ContractValueSnapshot { get; set; }

    public decimal DonationAmount { get; set; }

    /// <summary>Total paid to temples so far (rolls up from donation payments in P3; 0 until then).</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>
    /// Set when a contract-value change has recomputed <see cref="DonationAmount"/> and the
    /// existing temple split no longer totals it, or more has been paid than the new amount
    /// (P1-T07 acceptance — "flags any already-paid excess").
    /// </summary>
    public bool NeedsReview { get; set; }
}
