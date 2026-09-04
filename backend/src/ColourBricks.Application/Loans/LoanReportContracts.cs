namespace ColourBricks.Application.Loans;

public sealed record ProjectWiseLoanRowDto(
    long? ProjectId,
    string ProjectName,
    int LoanCount,
    decimal LoanAmount,
    decimal PrincipalPaid,
    decimal OutstandingPrincipal,
    decimal InterestPaid);

public sealed record LoanOutstandingReportRowDto(
    long LoanId,
    string LenderName,
    long? ProjectId,
    string ProjectName,
    decimal LoanAmount,
    decimal PrincipalPaid,
    decimal InterestPaid,
    decimal OutstandingPrincipal,
    DateOnly? NextDueDate,
    string Status);

public sealed record EmiPaidRowDto(
    long PaymentId,
    long LoanId,
    string LenderName,
    DateOnly Date,
    decimal Amount,
    decimal PrincipalPaid,
    decimal InterestPaid,
    bool IsPrepayment,
    string Status);

public sealed record EmiPendingRowDto(
    long LoanId,
    string LenderName,
    int InstalmentNo,
    DateOnly DueDate,
    decimal EmiAmount,
    decimal AmountDue,
    int DaysOverdue);

public sealed record PrincipalVsInterestRowDto(
    long LoanId,
    string LenderName,
    decimal LoanAmount,
    decimal PrincipalPaid,
    decimal InterestPaid,
    decimal OutstandingPrincipal);

public sealed record DateWiseEmiRowDto(
    DateOnly Date,
    int PaymentCount,
    decimal Amount,
    decimal PrincipalPaid,
    decimal InterestPaid);

/// <summary>P7-T05 — the seven BRD §54 loan reports.</summary>
public interface ILoanReportService
{
    Task<IReadOnlyList<ProjectWiseLoanRowDto>> ProjectWiseAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<LoanOutstandingReportRowDto>> OutstandingAsync(long? projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LoanEmiInstalmentDto>> ScheduleAsync(long loanId, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmiPaidRowDto>> EmiPaidAsync(
        long? loanId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmiPendingRowDto>> EmiPendingAsync(long? loanId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PrincipalVsInterestRowDto>> PrincipalVsInterestAsync(long? loanId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DateWiseEmiRowDto>> DateWisePaymentsAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
}
