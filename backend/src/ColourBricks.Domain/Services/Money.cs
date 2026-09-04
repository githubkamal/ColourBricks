namespace ColourBricks.Domain.Services;

/// <summary>
/// Money is <see cref="decimal"/> everywhere, rounded to <see cref="Scale"/> decimal
/// places with <see cref="MidpointRounding.AwayFromZero"/> (plan.md §5.4). Never
/// float/double. <see cref="Scale"/> is also the single source of truth for every
/// amount column's precision (<c>DECIMAL(18, Scale)</c>, set in
/// <c>AppDbContext.ConfigureConventions</c>) — changing it changes both together.
/// </summary>
public static class Money
{
    /// <summary>
    /// Decimal places every amount is rounded and stored to (client request,
    /// 2026-09-04 — raised from 2 to 3). Changing this requires a migration
    /// altering every decimal column to match (<c>DECIMAL(18, Scale)</c>).
    /// </summary>
    public const int Scale = 3;

    /// <summary>
    /// The smallest storable unit at the current <see cref="Scale"/> (e.g. one paisa
    /// at Scale=2, one milli-rupee at Scale=3) — the rounding-error tolerance for
    /// comparing a sum of already-rounded amounts against a target. Scales
    /// automatically if <see cref="Scale"/> ever changes again.
    /// </summary>
    public static readonly decimal Tolerance = 1m / (decimal)Math.Pow(10, Scale);

    /// <summary>
    /// A tenth of <see cref="Tolerance"/> — a pure floating-point-safety margin for
    /// "strictly greater than" comparisons between two already-rounded decimals,
    /// not a rounding-error allowance in its own right.
    /// </summary>
    public static readonly decimal Epsilon = Tolerance / 10m;

    public static decimal Round(decimal value) => Math.Round(value, Scale, MidpointRounding.AwayFromZero);

    /// <summary>True when the value has no fraction smaller than the smallest storable unit.</summary>
    public static bool IsWholeUnit(decimal value) => value == Round(value);
}
