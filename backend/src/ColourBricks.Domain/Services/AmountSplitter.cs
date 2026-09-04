namespace ColourBricks.Domain.Services;

/// <summary>
/// Splits a rupee amount across N targets so the parts sum <b>exactly</b> to the
/// whole (plan.md §5.4; BRD §45 equal distribution, §46 percentage distribution).
/// The first N-1 parts are floored to the paisa; the last part takes the remainder.
/// </summary>
public static class AmountSplitter
{
    /// <summary>Percentages may miss 100 by at most this much (2-decimal inputs).</summary>
    public const decimal PercentageTolerance = 0.01m;

    public static IReadOnlyList<decimal> SplitEqually(decimal amount, int targets)
    {
        if (targets < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(targets), targets, "At least one target is required.");
        }

        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount cannot be negative.");
        }

        decimal share = FloorToPaisa(amount / targets);

        var parts = new decimal[targets];
        for (int i = 0; i < targets - 1; i++)
        {
            parts[i] = share;
        }

        parts[targets - 1] = amount - (share * (targets - 1));
        return parts;
    }

    public static IReadOnlyList<decimal> SplitByPercentage(decimal amount, IReadOnlyList<decimal> percentages)
    {
        ArgumentNullException.ThrowIfNull(percentages);

        if (percentages.Count == 0)
        {
            throw new ArgumentException("At least one percentage is required.", nameof(percentages));
        }

        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount cannot be negative.");
        }

        if (percentages.Any(p => p < 0))
        {
            throw new ArgumentException("Percentages cannot be negative.", nameof(percentages));
        }

        decimal total = percentages.Sum();
        if (Math.Abs(total - 100m) > PercentageTolerance)
        {
            throw new ArgumentException(
                $"Percentages must sum to 100 within {PercentageTolerance} (got {total}).", nameof(percentages));
        }

        var parts = new decimal[percentages.Count];
        decimal allocated = 0m;
        for (int i = 0; i < percentages.Count - 1; i++)
        {
            parts[i] = FloorToPaisa(amount * percentages[i] / 100m);
            allocated += parts[i];
        }

        parts[^1] = amount - allocated;
        return parts;
    }

    private static decimal FloorToPaisa(decimal value) => Math.Floor(value * 100m) / 100m;
}
