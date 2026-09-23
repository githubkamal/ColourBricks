using ColourBricks.Domain.PurchaseOrders;

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

/// <param name="TaxRate">The GST rate (%), required when <paramref name="TaxType"/> is Percentage.</param>
/// <param name="TaxAmount">The flat tax figure, used as-is when <paramref name="TaxType"/> is Amount.</param>
public sealed record SubmitPurchaseOrderLineInput(
    long LineId,
    decimal Quantity,
    decimal Rate,
    PurchaseOrderTaxType TaxType = PurchaseOrderTaxType.Amount,
    decimal? TaxRate = null,
    decimal TaxAmount = 0m);

/// <summary>
/// An invoice charge that isn't one of the ordered items — transport, handling,
/// loading (client request, 2026-09-23). Optional; both the charge's tax and the
/// charge itself may be zero.
/// </summary>
/// <param name="TaxRate">The GST rate (%), required when <paramref name="TaxType"/> is Percentage.</param>
/// <param name="TaxAmount">The flat tax figure, used as-is when <paramref name="TaxType"/> is Amount.</param>
public sealed record PurchaseOrderChargeInput(
    string ChargeType,
    decimal Amount,
    PurchaseOrderTaxType TaxType = PurchaseOrderTaxType.Amount,
    decimal? TaxRate = null,
    decimal TaxAmount = 0m);

/// <summary>Every line must be priced — the whole order submits at once (client request, 2026-09-04).</summary>
/// <param name="Charges">
/// Order-wide extra charges (client request, 2026-09-23). Each is split across the
/// projects on the order in proportion to their item value, so a charge on a
/// multi-project order lands where the goods did.
/// </param>
/// <param name="RoundOff">
/// A manual adjustment to the invoice total, positive or negative (client request,
/// 2026-09-23). Applied whole to the project carrying the largest share of the
/// order rather than split — a round-off exists precisely to avoid fractions.
/// </param>
public sealed record SubmitPurchaseOrderRequest(
    string InvoiceNumber,
    IReadOnlyList<SubmitPurchaseOrderLineInput> Lines,
    IReadOnlyList<PurchaseOrderChargeInput>? Charges = null,
    decimal RoundOff = 0m);

public sealed record PurchaseOrderLineDto(
    long Id,
    long ProjectId,
    string ProjectName,
    long? ItemId,
    string ItemName,
    decimal Quantity,
    string Unit,
    decimal? Rate,
    /// <summary>Quantity * Rate, before GST — null until priced.</summary>
    decimal? Subtotal,
    PurchaseOrderTaxType TaxType,
    decimal? TaxRate,
    decimal? TaxAmount,
    decimal? LineTotal);

public sealed record PurchaseOrderChargeDto(
    long Id,
    string ChargeType,
    decimal Amount,
    PurchaseOrderTaxType TaxType,
    decimal? TaxRate,
    decimal TaxAmount,
    decimal Total);

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
    /// <summary>Sum of every line's Quantity * Rate, before GST.</summary>
    decimal SubtotalTotal,
    /// <summary>Sum of every line's GST amount.</summary>
    decimal TaxTotal,
    decimal Total,
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    /// <summary>Extra charges — transport, handling — with their own GST.</summary>
    IReadOnlyList<PurchaseOrderChargeDto> Charges,
    /// <summary>Sum of every charge's amount, before its GST.</summary>
    decimal ChargesSubtotal,
    /// <summary>Sum of every charge's GST amount.</summary>
    decimal ChargesTax,
    /// <summary>The manual adjustment applied to <see cref="Total"/>.</summary>
    decimal RoundOff,
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
