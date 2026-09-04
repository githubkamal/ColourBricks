using ColourBricks.Domain.Services;

namespace ColourBricks.Application.Reporting.Framework;

/// <summary>
/// Resolves a BRD §57 date preset to a concrete <see cref="DateRange"/> against a
/// given "today". Weeks run Monday–Sunday (ISO 8601). Pure — no clock, no DB, so it
/// is data-driven unit tested (P8-T01).
/// </summary>
public static class ReportDates
{
    public static DateRange Resolve(ReportDatePreset preset, DateOnly today, DateOnly? customFrom, DateOnly? customTo)
    {
        DateOnly monday = today.AddDays(-((int)today.DayOfWeek == 0 ? 6 : (int)today.DayOfWeek - 1));

        return preset switch
        {
            ReportDatePreset.Today => new DateRange(today, today),
            ReportDatePreset.Yesterday => new DateRange(today.AddDays(-1), today.AddDays(-1)),
            ReportDatePreset.ThisWeek => new DateRange(monday, monday.AddDays(6)),
            ReportDatePreset.PreviousWeek => new DateRange(monday.AddDays(-7), monday.AddDays(-1)),
            ReportDatePreset.ThisMonth => MonthOf(today.Year, today.Month),
            ReportDatePreset.PreviousMonth => MonthOf(today.AddMonths(-1).Year, today.AddMonths(-1).Month),
            ReportDatePreset.CurrentYear => new DateRange(new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31)),
            ReportDatePreset.PreviousYear => new DateRange(new DateOnly(today.Year - 1, 1, 1), new DateOnly(today.Year - 1, 12, 31)),
            ReportDatePreset.ThisFinancialYear => FyRange(FinancialYear.GetFinancialYear(today)),
            ReportDatePreset.PreviousFinancialYear => FyRange(FinancialYear.GetFinancialYear(today) - 1),
            _ => new DateRange(customFrom, customTo),
        };
    }

    private static DateRange MonthOf(int year, int month) =>
        new(new DateOnly(year, month, 1), new DateOnly(year, month, DateTime.DaysInMonth(year, month)));

    private static DateRange FyRange(int financialYear)
    {
        (DateOnly start, DateOnly end) = FinancialYear.FyRange(financialYear);
        return new DateRange(start, end);
    }
}
