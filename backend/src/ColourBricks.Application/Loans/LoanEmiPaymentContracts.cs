namespace ColourBricks.Application.Loans;

public sealed record LoanEmiPaymentDto(
    long Id,
    long LoanId,
    long? InstalmentId,
    DateOnly Date,
    decimal Amount,
    decimal PrincipalPaid,
    decimal InterestPaid,
    long PaymentModeId,
    long? AccountId,
    string? ReferenceNo,
    bool IsPrepayment,
    string Status,
    /// <summary>
    /// A reconciliation-anchor Settlement id, present only when the payment moved
    /// money through an account — lets the caller optionally link this payment to a
    /// bank transaction (<c>reconcile-debit</c>'s <c>existingPaymentId</c>) without
    /// this table itself needing to know anything about reconciliation (client
    /// request, 2026-09-06).
    /// </summary>
    long? SettlementId = null);

/// <summary>
/// Settle a scheduled instalment. <see cref="Amount"/> null pays the whole EMI; a
/// smaller amount is a part payment (applied to interest first, then principal).
/// </summary>
public sealed record PayEmiRequest(
    long InstalmentId,
    DateOnly Date,
    long PaymentModeId,
    decimal? Amount = null,
    long? AccountId = null,
    string? ReferenceNo = null);

/// <summary>
/// Pay extra principal ahead of schedule. The whole amount reduces the liability
/// and the pending instalments are re-amortised over the lower balance.
/// </summary>
public sealed record PrepayLoanRequest(
    decimal Amount,
    DateOnly Date,
    long PaymentModeId,
    long? AccountId = null,
    string? ReferenceNo = null);

public interface ILoanEmiPaymentService
{
    Task<LoanEmiPaymentDto> PayAsync(long loanId, PayEmiRequest request, CancellationToken cancellationToken);

    Task<LoanEmiPaymentDto> PrepayAsync(long loanId, PrepayLoanRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<LoanEmiPaymentDto>> ListAsync(long loanId, CancellationToken cancellationToken);

    Task<bool> ReverseAsync(long paymentId, string reason, CancellationToken cancellationToken);
}
