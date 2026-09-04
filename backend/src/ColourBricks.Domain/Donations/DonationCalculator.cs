using ColourBricks.Domain.Services;

namespace ColourBricks.Domain.Donations;

/// <summary>
/// Sizes a project's temple donation (BRD §26). Percentage basis: a share of the
/// project contract value, rounded to the paisa. Fixed basis: the stated amount.
/// </summary>
public static class DonationCalculator
{
    public static decimal Compute(
        DonationBasis basis, decimal? percentage, decimal? fixedAmount, decimal contractValue)
    {
        return basis switch
        {
            DonationBasis.Percentage => Money.Round(contractValue * (percentage ?? 0m) / 100m),
            DonationBasis.Fixed => Money.Round(fixedAmount ?? 0m),
            _ => 0m,
        };
    }
}
