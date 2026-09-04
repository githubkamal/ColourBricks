using System.Globalization;
using ColourBricks.Domain.Services;
using FluentAssertions;

namespace ColourBricks.UnitTests.Domain;

/// <summary>P7-T02 — reducing-balance EMI amortisation (BRD §48, §54).</summary>
public class EmiAmortisationTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "loans", name);

    [Fact]
    public void Amortisation_PrincipalSum_EqualsLoanAmount()
    {
        foreach ((decimal principal, decimal rate, int months) in new[]
                 {
                     (5_000_000m, 9m, 60),
                     (1_234_567m, 12.5m, 37),
                     (750_000m, 7.25m, 18),
                     (10_000_000m, 8m, 240),
                 })
        {
            IReadOnlyList<AmortisationRow> schedule = EmiAmortisation.Schedule(principal, rate, months);

            schedule.Sum(r => r.PrincipalComponent).Should().Be(principal);
            schedule[^1].ClosingPrincipal.Should().Be(0m);
        }
    }

    [Fact]
    public void Amortisation_MatchesReferenceTable_Within1Rupee()
    {
        var expected = File.ReadAllLines(FixturePath("emi_50L_9pct_60m.csv"))
            .Skip(1)
            .Select(line => line.Split(','))
            .Select(c => (
                No: int.Parse(c[0], CultureInfo.InvariantCulture),
                Emi: decimal.Parse(c[1], CultureInfo.InvariantCulture),
                Principal: decimal.Parse(c[2], CultureInfo.InvariantCulture),
                Interest: decimal.Parse(c[3], CultureInfo.InvariantCulture),
                Closing: decimal.Parse(c[4], CultureInfo.InvariantCulture)))
            .ToList();

        IReadOnlyList<AmortisationRow> actual = EmiAmortisation.Schedule(5_000_000m, 9m, 60);

        actual.Should().HaveCount(expected.Count);
        foreach (AmortisationRow row in actual)
        {
            var want = expected[row.InstalmentNo - 1];
            row.Emi.Should().BeApproximately(want.Emi, 1m);
            row.PrincipalComponent.Should().BeApproximately(want.Principal, 1m);
            row.InterestComponent.Should().BeApproximately(want.Interest, 1m);
            row.ClosingPrincipal.Should().BeApproximately(want.Closing, 1m);
        }
    }

    [Fact]
    public void Amortisation_ClientSuppliedEmi_FinalInstalmentAbsorbsDifference()
    {
        // A round client EMI below the computed ₹1,03,791.78 leaves a balloon last row.
        const decimal clientEmi = 100_000m;
        IReadOnlyList<AmortisationRow> schedule = EmiAmortisation.Schedule(5_000_000m, 9m, 60, clientEmi);

        schedule.Should().HaveCount(60);
        schedule.Take(59).Should().OnlyContain(r => r.Emi == clientEmi);
        schedule[^1].Emi.Should().NotBe(clientEmi);
        schedule.Sum(r => r.PrincipalComponent).Should().Be(5_000_000m);
        schedule[^1].ClosingPrincipal.Should().Be(0m);
    }

    [Fact]
    public void Amortisation_RateChange_PreservesPaidInstalments()
    {
        IReadOnlyList<AmortisationRow> original = EmiAmortisation.Schedule(5_000_000m, 9m, 60);
        const int paid = 12;

        IReadOnlyList<AmortisationRow> regenerated =
            EmiAmortisation.RegenerateSchedule(original, paid, newAnnualRatePercent: 10.5m);

        regenerated.Take(paid).Should().Equal(original.Take(paid));
        regenerated.Should().HaveCount(60);
        regenerated.Sum(r => r.PrincipalComponent).Should().Be(5_000_000m);
        // the tail was re-priced at the new rate
        regenerated[paid].Emi.Should().NotBe(original[paid].Emi);
        regenerated[^1].ClosingPrincipal.Should().Be(0m);
    }

    [Fact]
    public void Amortisation_ZeroInterest_DoesNotDivideByZero()
    {
        EmiAmortisation.ComputeEmi(1_200_000m, 0m, 12).Should().Be(100_000m);

        IReadOnlyList<AmortisationRow> schedule = EmiAmortisation.Schedule(1_200_000m, 0m, 12);

        schedule.Should().HaveCount(12);
        schedule.Should().OnlyContain(r => r.InterestComponent == 0m);
        schedule.Sum(r => r.PrincipalComponent).Should().Be(1_200_000m);
    }
}
