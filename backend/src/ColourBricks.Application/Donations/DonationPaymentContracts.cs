namespace ColourBricks.Application.Donations;

public sealed record PayDonationRequest(
    DateOnly Date,
    decimal Amount,
    long PaymentModeId,
    long? AccountId = null,
    string? ReferenceNo = null);

public sealed record DonationTempleOutstandingDto(
    long TempleId,
    string TempleName,
    decimal Allocated,
    decimal Paid,
    decimal Outstanding);

public sealed record DonationPaymentDto(
    long Id,
    long ProjectId,
    long TempleId,
    DateOnly Date,
    decimal Amount,
    long PaymentModeId,
    long? AccountId,
    string? ReferenceNo);

public interface IDonationPaymentService
{
    Task<DonationPaymentDto> PayAsync(
        long projectId, long templeId, PayDonationRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<DonationTempleOutstandingDto>> OutstandingByTempleAsync(
        long projectId, CancellationToken cancellationToken);
}
