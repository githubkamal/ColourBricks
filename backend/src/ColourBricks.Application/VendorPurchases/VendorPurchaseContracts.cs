namespace ColourBricks.Application.VendorPurchases;

public sealed record PurchaseLineInput(
    long? ItemId,
    string ItemName,
    decimal Quantity,
    string Unit,
    decimal Rate,
    decimal TaxAmount = 0m);

public sealed record PurchaseLineDto(
    long Id,
    long? ItemId,
    string ItemName,
    decimal Quantity,
    string Unit,
    decimal Rate,
    decimal TaxAmount,
    decimal LineTotal);

/// <param name="RoundOff">
/// A manual adjustment to the invoice total, positive or negative (client request,
/// 2026-09-23). <paramref name="Total"/> must equal Σ line totals + this, so a
/// purchase with no round-off behaves exactly as it always has.
/// </param>
public sealed record RecordVendorPurchaseRequest(
    long ProjectId,
    long VendorId,
    DateOnly Date,
    decimal Total,
    IReadOnlyList<PurchaseLineInput> Lines,
    string? InvoiceNumber = null,
    string? Description = null,
    decimal? PartPayment = null,
    long? PartPaymentModeId = null,
    long? PartPaymentAccountId = null,
    string? PartPaymentReference = null,
    decimal RoundOff = 0m);

public sealed record VendorPurchaseDto(
    long Id,
    long ProjectId,
    long VendorId,
    string VendorName,
    DateOnly Date,
    string? InvoiceNumber,
    decimal Total,
    decimal PartPaid,
    decimal VendorOutstandingAfter,
    string Status,
    IReadOnlyList<PurchaseLineDto> Lines,
    /// <summary>The manual adjustment folded into <see cref="Total"/>; zero when none.</summary>
    decimal RoundOff = 0m);

public sealed record RecordVendorPurchaseResult(VendorPurchaseDto Purchase, bool DuplicateInvoiceWarning);
