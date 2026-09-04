namespace ColourBricks.Application.Allocations;

/// <summary>One project (or advance) slice of a settlement's allocation (BRD §23).</summary>
public sealed record AllocationHistoryLineDto(
    long? ProjectId,
    string ProjectName,
    long? ObligationId,
    string? ObligationReference,
    decimal Amount,
    string Method);

/// <summary>The full allocation of one settlement — the drill-through target from a ledger line.</summary>
public sealed record SettlementAllocationHistoryDto(
    long SettlementId,
    DateOnly Date,
    long VendorId,
    string VendorName,
    decimal TotalPayment,
    string Status,
    IReadOnlyList<AllocationHistoryLineDto> Lines);

/// <summary>One row of the BRD §51 Vendor Payment Allocation Report.</summary>
public sealed record VendorPaymentAllocationReportRowDto(
    DateOnly Date,
    long VendorId,
    string VendorName,
    long SettlementId,
    decimal TotalPayment,
    long? ProjectId,
    string ProjectName,
    decimal Allocated,
    string Method,
    string Status);

public sealed record VendorPaymentAllocationReportQuery(
    long? VendorId = null,
    long? ProjectId = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null);

public interface IAllocationHistoryService
{
    Task<SettlementAllocationHistoryDto?> ForSettlementAsync(
        long settlementId, CancellationToken cancellationToken);

    Task<IReadOnlyList<VendorPaymentAllocationReportRowDto>> ReportAsync(
        VendorPaymentAllocationReportQuery query, CancellationToken cancellationToken);
}
