namespace ColourBricks.Application.Outstanding;

public sealed record ProjectOutstandingLineDto(long ProjectId, string ProjectName, decimal Outstanding);

public sealed record PartyOutstandingLineDto(long PartyId, string PartyName, decimal Outstanding);

public sealed record VendorOutstandingSummaryDto(
    long VendorId,
    decimal Total,
    IReadOnlyList<ProjectOutstandingLineDto> ByProject,
    decimal Advance = 0m);

public sealed record ProjectOutstandingSummaryDto(
    long ProjectId,
    decimal VendorPayable,
    decimal SubcontractorPayable,
    decimal CustomWorkPayable,
    decimal DonationPayable,
    decimal ClientReceivable,
    decimal TotalPayable);

public sealed record AgeingBucketsDto(
    decimal Current,     // 0–30 days
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90,
    decimal Total);

/// <summary>
/// The single read-side for "who owes whom, and how much" (BRD §17, §21, §38).
/// Every figure is derived from ledger payable entries — nothing is stored — and
/// reversed obligations/settlements fall out automatically because their mirrored
/// ledger entries net to zero.
/// </summary>
public interface IOutstandingService
{
    Task<decimal> VendorTotalAsync(long vendorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectOutstandingLineDto>> VendorByProjectAsync(
        long vendorId, CancellationToken cancellationToken);

    Task<VendorOutstandingSummaryDto> VendorSummaryAsync(long vendorId, CancellationToken cancellationToken);

    Task<decimal> SubcontractorTotalAsync(long teamId, CancellationToken cancellationToken);

    Task<decimal> ProjectTotalPayableAsync(long projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartyOutstandingLineDto>> ProjectByVendorAsync(
        long projectId, CancellationToken cancellationToken);

    Task<decimal> DonationOutstandingForProjectAsync(long projectId, CancellationToken cancellationToken);

    Task<decimal> ClientOutstandingAsync(long projectId, CancellationToken cancellationToken);

    Task<ProjectOutstandingSummaryDto> ProjectSummaryAsync(long projectId, CancellationToken cancellationToken);

    /// <summary>FIFO-attributed ageing of a vendor's unpaid purchases into 30-day buckets.</summary>
    Task<AgeingBucketsDto> VendorAgeingAsync(long vendorId, CancellationToken cancellationToken);
}
