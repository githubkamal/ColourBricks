using ColourBricks.Domain.Donations;

namespace ColourBricks.Application.Donations;

public sealed record DonationTempleSplitDto(long TempleId, string TempleName, decimal Amount);

/// <summary>
/// A project's temple-donation setup (BRD §26). <see cref="DonationAmount"/> is
/// computed server-side; <see cref="ExcessPaid"/> and <see cref="NeedsReview"/>
/// surface a stale split after a contract-value change.
/// </summary>
public sealed record ProjectDonationDto(
    long Id,
    long ProjectId,
    DonationBasis Basis,
    decimal? Percentage,
    decimal? FixedAmount,
    decimal ContractValueSnapshot,
    decimal DonationAmount,
    decimal PaidAmount,
    decimal ExcessPaid,
    bool NeedsReview,
    IReadOnlyList<DonationTempleSplitDto> Temples,
    string ConcurrencyStamp);

public sealed record DonationTempleSplitInput(long TempleId, decimal Amount);

public sealed record UpsertProjectDonationRequest(
    DonationBasis Basis,
    decimal? Percentage,
    decimal? FixedAmount,
    IReadOnlyList<DonationTempleSplitInput> Temples);
