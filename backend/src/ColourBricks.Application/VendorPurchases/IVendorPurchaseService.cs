namespace ColourBricks.Application.VendorPurchases;

public interface IVendorPurchaseService
{
    Task<RecordVendorPurchaseResult> RecordAsync(
        RecordVendorPurchaseRequest request, CancellationToken cancellationToken);

    Task<VendorPurchaseDto?> GetAsync(long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<VendorPurchaseDto>> ListAsync(
        long? projectId, long? vendorId, CancellationToken cancellationToken);

    /// <summary>Σ(payable credits − debits) for a vendor across all projects.</summary>
    Task<decimal> VendorOutstandingAsync(long vendorId, CancellationToken cancellationToken);

    /// <summary>
    /// Reverses the purchase's ledger posting (and any inline part-payment) and marks
    /// the obligation <c>Reversed</c>. Returns false if the purchase does not exist.
    /// </summary>
    Task<bool> ReverseAsync(long id, string reason, CancellationToken cancellationToken);
}
