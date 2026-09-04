namespace ColourBricks.Application.Loans;

public sealed record LoanDto(
    long Id,
    long? ProjectId,
    long LenderId,
    string LenderName,
    decimal PrincipalAmount,
    decimal AnnualInterestRatePercent,
    DateOnly StartDate,
    int TenureMonths,
    decimal? EmiAmount,
    DateOnly EmiStartDate,
    DateOnly? EmiEndDate,
    long DisbursementAccountId,
    DateOnly DisbursementDate,
    string? Reference,
    string? Notes,
    string Status,
    // Derived: disbursed principal minus principal repaid. Never stored (BRD §48).
    decimal OutstandingPrincipal);

public sealed record RecordLoanRequest(
    long LenderId,
    decimal PrincipalAmount,
    decimal AnnualInterestRatePercent,
    DateOnly StartDate,
    int TenureMonths,
    DateOnly EmiStartDate,
    long DisbursementAccountId,
    DateOnly DisbursementDate,
    long? ProjectId = null,
    decimal? EmiAmount = null,
    string? Reference = null,
    string? Notes = null);

public interface ILoanService
{
    Task<LoanDto> RecordAsync(RecordLoanRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<LoanDto>> ListAsync(long? projectId, long? lenderId, CancellationToken cancellationToken);

    Task<LoanDto?> GetAsync(long id, CancellationToken cancellationToken);

    Task<bool> ReverseAsync(long id, string reason, CancellationToken cancellationToken);
}
