namespace ColourBricks.Application.Loans;

public sealed record LoanEmiInstalmentDto(
    long Id,
    long LoanId,
    int InstalmentNo,
    DateOnly DueDate,
    decimal OpeningPrincipal,
    decimal EmiAmount,
    decimal PrincipalComponent,
    decimal InterestComponent,
    decimal ClosingPrincipal,
    string Status,
    decimal PaidAmount,
    DateOnly? PaidDate,
    bool Overdue);

/// <summary>Generate (or replace, when no instalment is paid) a loan's schedule.</summary>
public sealed record GenerateLoanScheduleRequest(decimal? EmiAmount = null);

/// <summary>Re-price the unpaid tail at a new rate, leaving paid instalments intact.</summary>
public sealed record RegenerateLoanScheduleRequest(decimal NewAnnualRatePercent, decimal? EmiAmount = null);

public interface ILoanScheduleService
{
    Task<IReadOnlyList<LoanEmiInstalmentDto>> GenerateAsync(
        long loanId, GenerateLoanScheduleRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<LoanEmiInstalmentDto>> RegenerateAsync(
        long loanId, RegenerateLoanScheduleRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<LoanEmiInstalmentDto>> GetAsync(long loanId, CancellationToken cancellationToken);

    /// <summary>
    /// Re-amortise the pending tail against the loan's current outstanding principal
    /// and rate — used after a prepayment (P7-T03). Paid / part-paid instalments are
    /// left untouched.
    /// </summary>
    Task<IReadOnlyList<LoanEmiInstalmentDto>> RebuildPendingTailAsync(long loanId, CancellationToken cancellationToken);
}
