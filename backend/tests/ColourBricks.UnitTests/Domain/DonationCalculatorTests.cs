using ColourBricks.Domain.Donations;
using FluentAssertions;

namespace ColourBricks.UnitTests.Domain;

public class DonationCalculatorTests
{
    [Fact]
    public void Percentage_TwoPercentOfOneCrore_IsTwoLakh()
    {
        // BRD §26 worked example.
        DonationCalculator.Compute(DonationBasis.Percentage, percentage: 2m, fixedAmount: null, contractValue: 10_000_000m)
            .Should().Be(200_000m);
    }

    [Fact]
    public void Percentage_RoundsToTheSmallestStorableUnit()
    {
        // Money.Scale is 3 (client request, 2026-09-04) — 1,000,000.05 * 1% = 10,000.0005,
        // a midpoint at the 4th decimal that rounds away from zero to 10,000.001.
        DonationCalculator.Compute(DonationBasis.Percentage, percentage: 1m, fixedAmount: null, contractValue: 1_000_000.05m)
            .Should().Be(10_000.001m);
    }

    [Fact]
    public void Fixed_ReturnsTheStatedAmount_IgnoringContractValue()
    {
        DonationCalculator.Compute(DonationBasis.Fixed, percentage: null, fixedAmount: 75_000m, contractValue: 9_999_999m)
            .Should().Be(75_000m);
    }
}
