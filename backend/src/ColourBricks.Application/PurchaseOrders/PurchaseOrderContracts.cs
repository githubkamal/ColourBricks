namespace ColourBricks.Application.PurchaseOrders;

public sealed record PurchaseOrderLineInput(
    long ProjectId,
    long? ItemId,
    string ItemName,
    decimal Quantity,
    string Unit);

public sealed record CreatePurchaseOrderRequest(
    long VendorId,
    DateOnly OrderDate,
    IReadOnlyList<PurchaseOrderLineInput> Lines,
    string? Notes = null);

/// <summary>Full replace of a draft order's lines. Draft only.</summary>
public sealed record UpdatePurchaseOrderRequest(
    DateOnly OrderDate,
    IReadOnlyList<PurchaseOrderLineInput> Lines,
    string ConcurrencyStamp,
    string? Notes = null);

public sealed record SubmitPurchaseOrderLineInput(
    long LineId,
    decimal Quantity,
    decimal Rate,
    decimal TaxAmount = 0m);

/// <summary>Every line must be priced — the whole order submits at once (client request, 2026-09-04).</summary>
public sealed record SubmitPurchaseOrderRequest(
    string InvoiceNumber,
    IReadOnlyList<SubmitPurchaseOrderLineInput> Lines);

public sealed record PurchaseOrderLineDto(
    long Id,
    long ProjectId,
    string ProjectName,
    long? ItemId,
    string ItemName,
    decimal Quantity,
    string Unit,
    decimal? Rate,
    decimal? TaxAmount,
    decimal? LineTotal);

public sealed record PurchaseOrderDto(
    long Id,
    string PoNumber,
    long VendorId,
    string VendorName,
    DateOnly OrderDate,
    string Status,
    string? InvoiceNumber,
    DateOnly? SubmittedDate,
    string? Notes,
    decimal Total,
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    /// <summary>The vendor purchase(s) this order became on submit — one per project involved.</summary>
    IReadOnlyList<long> ObligationIds,
    string ConcurrencyStamp);

public interface IPurchaseOrderService
{
    Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken);

    Task<PurchaseOrderDto?> GetAsync(long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<PurchaseOrderDto>> ListAsync(
        long? vendorId, string? status, CancellationToken cancellationToken);

    /// <summary>Replaces the line set of a Draft order. Throws if the order is not Draft.</summary>
    Task<PurchaseOrderDto?> UpdateAsync(long id, UpdatePurchaseOrderRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Prices every line, splits by project and posts one vendor purchase per
    /// project — each reaching that project's expenses and the vendor's
    /// outstanding through the ordinary vendor-purchase path.
    /// </summary>
    Task<PurchaseOrderDto> SubmitAsync(long id, SubmitPurchaseOrderRequest request, CancellationToken cancellationToken);

    /// <summary>Cancels a Draft order. Nothing has posted yet, so this is a status flip, not a reversal.</summary>
    Task<bool> CancelAsync(long id, CancellationToken cancellationToken);
}
