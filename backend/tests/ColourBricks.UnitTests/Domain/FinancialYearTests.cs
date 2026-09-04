using ColourBricks.Domain.Services;
using FluentAssertions;

namespace ColourBricks.UnitTests.Domain;

public class FinancialYearTests
{
    [Fact]
    public void FinancialYear_March31_And_April1_LandInDifferentYears()
    {
        FinancialYear.GetFinancialYear(new DateOnly(2026, 3, 31)).Should().Be(2025);
        FinancialYear.GetFinancialYear(new DateOnly(2026, 4, 1)).Should().Be(2026);
    }

    [Fact]
    public void FyStart_And_FyEnd_SpanAprilToMarch()
    {
        FinancialYear.FyStart(2026).Should().Be(new DateOnly(2026, 4, 1));
        FinancialYear.FyEnd(2026).Should().Be(new DateOnly(2027, 3, 31));
        FinancialYear.Label(2026).Should().Be("2026-27");
    }

    [Fact]
    public void Money_Round_UsesAwayFromZero()
    {
        // Money.Scale is 3 (client request, 2026-09-04, raised from 2) — these exercise
        // the midpoint at the 4th decimal, the boundary Money.Round now actually rounds.
        Money.Round(2.3455m).Should().Be(2.346m);
        Money.Round(2.3454m).Should().Be(2.345m);
    }
}
