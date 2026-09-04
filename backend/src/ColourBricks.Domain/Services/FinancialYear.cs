namespace ColourBricks.Domain.Services;

/// <summary>
/// Indian financial year, April to March (plan.md §5.5). A financial year is
/// identified by its starting calendar year: FY 2026 runs 1 Apr 2026 – 31 Mar 2027.
/// </summary>
public static class FinancialYear
{
    public const int StartMonth = 4;

    /// <summary>The starting year of the financial year that contains <paramref name="date"/>.</summary>
    public static int GetFinancialYear(DateOnly date) =>
        date.Month >= StartMonth ? date.Year : date.Year - 1;

    public static DateOnly FyStart(int financialYear) => new(financialYear, StartMonth, 1);

    public static DateOnly FyEnd(int financialYear) => new(financialYear + 1, StartMonth - 1, 31);

    public static (DateOnly Start, DateOnly End) FyRange(int financialYear) =>
        (FyStart(financialYear), FyEnd(financialYear));

    /// <summary>e.g. "2026-27".</summary>
    public static string Label(int financialYear) => $"{financialYear}-{(financialYear + 1) % 100:D2}";
}
