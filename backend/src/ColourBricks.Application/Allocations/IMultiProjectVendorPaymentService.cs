namespace ColourBricks.Application.Allocations;

public sealed record RecordMultiProjectPaymentRequest(
    long VendorId,
    DateOnly Date,
    decimal Amount,
    long PaymentModeId,
    long? AccountId = null,
    string? ReferenceNo = null,
    IReadOnlyList<AllocationInputDto>? Allocations = null,
    string? OverrideReason = null,
    /// <summary>
    /// An explicit project-less portion of this payment (client request, 2026-09-04 —
    /// bank-reconciliation "map to vendor, no project"). Added to whatever FIFO
    /// overflow would already become an advance; a manual <see cref="Allocations"/>
    /// set plus this must total exactly <see cref="Amount"/>.
    /// </summary>
    decimal AdvanceAmount = 0m);

public sealed record MultiProjectPaymentDto(
    long SettlementId,
    long VendorId,
    decimal Amount,
    string Method,
    IReadOnlyList<AllocationLineDto> Applied,
    decimal Advance = 0m);

public interface IMultiProjectVendorPaymentService
{
    Task<AllocationProposalDto> ProposeAsync(long vendorId, decimal amount, CancellationToken cancellationToken);

    Task<MultiProjectPaymentDto> PayAsync(
        RecordMultiProjectPaymentRequest request, CancellationToken cancellationToken);
}
