namespace ColourBricks.Application.CommonExpenses;

public sealed record CommonExpenseDto(
    long Id,
    string Type,
    string SubCategory,
    DateOnly Date,
    decimal Amount,
    long PaymentModeId,
    long? AccountId,
    string? ReferenceNo,
    string? Description,
    string Status);

public sealed record RecordCommonExpenseRequest(
    string Type,
    string SubCategory,
    DateOnly Date,
    decimal Amount,
    long PaymentModeId,
    long? AccountId = null,
    string? ReferenceNo = null,
    string? Description = null);

public sealed record CommonExpenseQuery(
    string? Type = null,
    string? SubCategory = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    long? PaymentModeId = null,
    long? AccountId = null);

/// <summary>
/// Company-level totals for a period. <see cref="Total"/> is expense only
/// (Personal + Office + Custom); Savings is separate, not a P&amp;L expense.
/// </summary>
public sealed record CommonExpenseSummaryDto(
    decimal Personal,
    decimal Office,
    decimal Savings,
    decimal Total,
    decimal Custom = 0m);

public interface ICommonExpenseService
{
    Task<CommonExpenseDto> RecordAsync(RecordCommonExpenseRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<CommonExpenseDto>> ListAsync(CommonExpenseQuery query, CancellationToken cancellationToken);

    Task<CommonExpenseSummaryDto> SummaryAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    /// <summary>
    /// Reverses a common expense (client request, 2026-09-04 — bank-reconciliation
    /// unmap/undo of a Personal/Office/Savings map). Mirrors <c>ReceiptService.ReverseAsync</c>:
    /// reverses the ledger posting and marks the row Reversed. Returns false if the id
    /// does not exist.
    /// </summary>
    Task<bool> ReverseAsync(long id, string reason, CancellationToken cancellationToken);
}
