namespace ColourBricks.Application.VendorPayments;

public sealed record VendorPaymentDto(
    long Id,
    long VendorId,
    long ProjectId,
    DateOnly Date,
    decimal Amount,
    long PaymentModeId,
    long? AccountId,
    string? ReferenceNo,
    string Status,
    decimal VendorProjectOutstandingAfter,
    decimal AdvanceCreated = 0m);

public sealed record RecordVendorPaymentRequest(
    long VendorId,
    long? ProjectId,
    DateOnly Date,
    decimal Amount,
    long PaymentModeId,
    long? AccountId = null,
    string? ReferenceNo = null,
    IReadOnlyList<long>? ObligationIds = null);

public sealed record VendorStatementRowDto(
    DateOnly Date,
    string Kind,          // "Purchase" | "Payment" | "Advance"
    string Reference,
    decimal PurchaseAmount,
    decimal Paid,
    decimal RunningOutstanding);

/// <summary>Apply an existing vendor advance against an open purchase (BRD §25, P3-T05). No cash moves.</summary>
public sealed record ApplyVendorAdvanceRequest(
    long VendorId,
    long ObligationId,
    decimal Amount,
    DateOnly Date);

public sealed record ApplyVendorAdvanceDto(
    long ObligationId,
    decimal Applied,
    decimal AdvanceRemaining,
    decimal ObligationOutstandingAfter);

public interface IVendorPaymentService
{
    Task<VendorPaymentDto> PayAsync(RecordVendorPaymentRequest request, CancellationToken cancellationToken);

    Task<ApplyVendorAdvanceDto> ApplyAdvanceAsync(
        ApplyVendorAdvanceRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<VendorPaymentDto>> ListForVendorAsync(
        long vendorId, long? projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<VendorStatementRowDto>> StatementAsync(long vendorId, CancellationToken cancellationToken);

    Task<bool> ReverseAsync(long paymentId, string reason, CancellationToken cancellationToken);
}
