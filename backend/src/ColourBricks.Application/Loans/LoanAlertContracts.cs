namespace ColourBricks.Application.Loans;

public sealed record LoanAlertDto(
    long Id,
    long LoanId,
    long InstalmentId,
    int InstalmentNo,
    string Kind,
    DateOnly DueDate,
    decimal Amount,
    int DaysFromToday);

public sealed record LoanAlertsDto(
    int DaysAhead,
    IReadOnlyList<LoanAlertDto> Upcoming,
    IReadOnlyList<LoanAlertDto> Overdue);

public sealed record LoanOutstandingRowDto(
    long? ProjectId,
    string ProjectName,
    int LoanCount,
    decimal PrincipalOutstanding);

public sealed record LoanOutstandingSummaryDto(
    decimal CompanyPrincipalOutstanding,
    IReadOnlyList<LoanOutstandingRowDto> ByProject);

public interface ILoanAlertService
{
    /// <summary>
    /// Classify every unpaid instalment into upcoming (due within
    /// <paramref name="daysAhead"/>) or overdue, persisting one alert row per
    /// (instalment, kind) so repeated runs raise nothing new, and resolving alerts
    /// whose instalment is now paid. Returns the currently unresolved alerts.
    /// </summary>
    Task<LoanAlertsDto> RunAsync(int daysAhead, CancellationToken cancellationToken);

    Task<LoanOutstandingSummaryDto> OutstandingSummaryAsync(CancellationToken cancellationToken);
}
