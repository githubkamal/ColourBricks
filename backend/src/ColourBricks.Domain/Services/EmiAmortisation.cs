namespace ColourBricks.Domain.Services;

/// <summary>One row of a reducing-balance amortisation schedule.</summary>
public sealed record AmortisationRow(
    int InstalmentNo,
    decimal OpeningPrincipal,
    decimal Emi,
    decimal PrincipalComponent,
    decimal InterestComponent,
    decimal ClosingPrincipal);

/// <summary>
/// Standard reducing-balance EMI amortisation (BRD §48, §54):
/// <c>EMI = P·r·(1+r)^n / ((1+r)^n − 1)</c>, with <c>r</c> the monthly rate. A zero
/// rate degrades to <c>P / n</c>. The schedule always repays the principal
/// <b>exactly</b> — the final instalment absorbs rounding and any gap between a
/// client-supplied EMI and the computed one.
/// </summary>
public static class EmiAmortisation
{
    /// <summary>The computed EMI, rounded to the paisa. <paramref name="annualRatePercent"/> is a percentage (9 = 9%).</summary>
    public static decimal ComputeEmi(decimal principal, decimal annualRatePercent, int months)
    {
        if (principal <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(principal), principal, "Principal must be positive.");
        }

        if (months < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(months), months, "Tenure must be at least one month.");
        }

        if (annualRatePercent < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(annualRatePercent), annualRatePercent, "Rate cannot be negative.");
        }

        decimal r = annualRatePercent / 1200m;
        if (r == 0m)
        {
            return Money.Round(principal / months);
        }

        decimal factor = Pow1p(r, months);
        return Money.Round(principal * r * factor / (factor - 1m));
    }

    /// <summary>
    /// The full schedule. <paramref name="clientEmi"/>, when given, replaces the
    /// computed EMI for every instalment but the last, which is recomputed so the
    /// closing balance lands on zero.
    /// </summary>
    public static IReadOnlyList<AmortisationRow> Schedule(
        decimal principal, decimal annualRatePercent, int months, decimal? clientEmi = null)
    {
        decimal r = annualRatePercent / 1200m;
        decimal emi = clientEmi is { } supplied
            ? Money.Round(supplied)
            : ComputeEmi(principal, annualRatePercent, months);

        if (emi <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(clientEmi), clientEmi, "EMI must be positive.");
        }

        var rows = new List<AmortisationRow>(months);
        decimal balance = Money.Round(principal);

        for (int k = 1; k <= months && balance > 0m; k++)
        {
            decimal interest = Money.Round(balance * r);
            decimal scheduledPrincipal = emi - interest;

            bool finalRow = k == months || scheduledPrincipal >= balance;
            decimal principalComponent = finalRow ? balance : scheduledPrincipal;
            decimal rowEmi = finalRow ? principalComponent + interest : emi;
            decimal closing = balance - principalComponent;

            rows.Add(new AmortisationRow(k, balance, rowEmi, principalComponent, interest, closing));
            balance = closing;

            if (finalRow)
            {
                break;
            }
        }

        return rows;
    }

    /// <summary>
    /// Rebuilds the tail of a schedule after a rate change, leaving the first
    /// <paramref name="paidCount"/> instalments untouched. The new tail amortises the
    /// balance outstanding after the last paid instalment over the remaining months
    /// (BRD §48 — regeneration preserves paid instalments).
    /// </summary>
    public static IReadOnlyList<AmortisationRow> RegenerateSchedule(
        IReadOnlyList<AmortisationRow> current,
        int paidCount,
        decimal newAnnualRatePercent,
        decimal? clientEmi = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (paidCount < 0 || paidCount > current.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(paidCount), paidCount, "Paid count is outside the schedule.");
        }

        var kept = current.Take(paidCount).ToList();
        int remaining = current.Count - paidCount;
        if (remaining == 0)
        {
            return kept;
        }

        decimal outstanding = paidCount == 0 ? current[0].OpeningPrincipal : current[paidCount - 1].ClosingPrincipal;
        if (outstanding <= 0m)
        {
            return kept;
        }

        var tail = Schedule(outstanding, newAnnualRatePercent, remaining, clientEmi)
            .Select(row => row with { InstalmentNo = row.InstalmentNo + paidCount })
            .ToList();

        kept.AddRange(tail);
        return kept;
    }

    /// <summary><c>(1 + r)^n</c> by repeated multiplication — keeps full decimal precision for n ≤ a few thousand.</summary>
    private static decimal Pow1p(decimal r, int n)
    {
        decimal result = 1m;
        decimal baseValue = 1m + r;
        for (int i = 0; i < n; i++)
        {
            result *= baseValue;
        }

        return result;
    }
}
