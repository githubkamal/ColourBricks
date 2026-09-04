using ColourBricks.Domain.Services;
using FluentAssertions;

namespace ColourBricks.UnitTests.Domain;

public class AmountSplitterTests
{
    [Fact]
    public void SplitEqually_100000By3_MatchesExpected()
    {
        AmountSplitter.SplitEqually(100_000m, 3)
            .Should().Equal(33_333.33m, 33_333.33m, 33_333.34m);
    }

    [Fact]
    public void SplitEqually_100By7_FloorsFirstAndRemaindersLast()
    {
        IReadOnlyList<decimal> parts = AmountSplitter.SplitEqually(100m, 7);

        parts.Take(6).Should().OnlyContain(p => p == 14.28m);
        parts[^1].Should().Be(14.32m);
        parts.Sum().Should().Be(100m);
    }

    [Fact]
    public void SplitEqually_ZeroTargets_Throws()
    {
        FluentActions.Invoking(() => AmountSplitter.SplitEqually(100m, 0))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SplitEqually_PartsSumToWhole()
    {
        var random = new Random(20260902);

        for (int iteration = 0; iteration < 5_000; iteration++)
        {
            decimal amount = Math.Round((decimal)(random.NextDouble() * 10_000_000d), 2);
            int targets = random.Next(1, 21);

            IReadOnlyList<decimal> parts = AmountSplitter.SplitEqually(amount, targets);

            parts.Should().HaveCount(targets);
            parts.Sum().Should().Be(amount, "parts must sum exactly to the whole");
            parts.Should().OnlyContain(p => p >= 0m && p == Money.Round(p));
        }
    }

    [Fact]
    public void SplitByPercentage_NotSummingTo100_Throws()
    {
        FluentActions.Invoking(() => AmountSplitter.SplitByPercentage(1_000m, [40m, 30m, 20m]))
            .Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(new[] { 40.0, 30.0, 20.0, 10.0 })]
    [InlineData(new[] { 33.33, 33.33, 33.34 })]
    [InlineData(new[] { 100.0 })]
    public void SplitByPercentage_PartsSumToWhole(double[] percentages)
    {
        decimal[] pct = percentages.Select(p => (decimal)p).ToArray();

        IReadOnlyList<decimal> parts = AmountSplitter.SplitByPercentage(100_000m, pct);

        parts.Should().HaveCount(pct.Length);
        parts.Sum().Should().Be(100_000m);
    }
}
