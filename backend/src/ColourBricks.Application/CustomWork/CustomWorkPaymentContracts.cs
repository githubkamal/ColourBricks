namespace ColourBricks.Application.CustomWork;

/// <summary>
/// Settles a custom-work obligation (client request, 2026-09-04 — product validation
/// found custom work had no way to be paid, reversed, or matched against a bank
/// statement, unlike every other payable type). Single project + party, no advance
/// overflow: paying more than is outstanding is rejected rather than silently parked
/// as a credit, since nothing asked for a custom-work "advance" concept.
/// </summary>
public sealed record RecordCustomWorkPaymentRequest(
    long ProjectId,
    long PartyId,
    DateOnly Date,
    decimal Amount,
    long PaymentModeId,
    long? AccountId = null,
    string? ReferenceNo = null,
    /// <summary>
    /// Ties this payment to one specific custom-work record, so reversing that record
    /// later (<see cref="ICustomWorkService.ReverseAsync"/>) also reverses this payment —
    /// mirrors <c>RecordVendorPaymentRequest.ObligationIds</c>. Optional: omit to pay
    /// down the project+party's aggregate outstanding instead.
    /// </summary>
    long? CustomWorkId = null);

public sealed record CustomWorkPaymentDto(
    long Id,
    long ProjectId,
    long PartyId,
    DateOnly Date,
    decimal Amount,
    long PaymentModeId,
    long? AccountId,
    string? ReferenceNo,
    string Status,
    decimal OutstandingAfter);

public interface ICustomWorkPaymentService
{
    Task<CustomWorkPaymentDto> PayAsync(RecordCustomWorkPaymentRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomWorkPaymentDto>> ListAsync(
        long projectId, long partyId, CancellationToken cancellationToken);

    /// <summary>Mirrors <c>VendorPaymentService.ReverseAsync</c>. Returns false if the id does not exist.</summary>
    Task<bool> ReverseAsync(long paymentId, string reason, CancellationToken cancellationToken);
}
