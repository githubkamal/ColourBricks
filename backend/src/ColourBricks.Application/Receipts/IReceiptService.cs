namespace ColourBricks.Application.Receipts;

public interface IReceiptService
{
    Task<ReceiptDto> RecordAsync(RecordReceiptRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReceiptDto>> ListAsync(
        long projectId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    Task<ReceiptDto?> GetAsync(long id, CancellationToken cancellationToken);

    /// <summary>Reverses the ledger posting and marks the receipt <c>Reversed</c>. False if not found.</summary>
    Task<bool> ReverseAsync(long id, string reason, CancellationToken cancellationToken);

    /// <summary>Sum of active receipt amounts for a project (BRD §5 "Total Income").</summary>
    Task<decimal> TotalActiveIncomeAsync(long projectId, CancellationToken cancellationToken);
}
