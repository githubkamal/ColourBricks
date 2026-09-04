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
    string? PartPaymentReference = null);

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
    IReadOnlyList<PurchaseLineDto> Lines);

public sealed record RecordVendorPurchaseResult(VendorPurchaseDto Purchase, bool DuplicateInvoiceWarning);
