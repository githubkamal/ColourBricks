using ColourBricks.Api.Authorization;
using ColourBricks.Application.Allocations;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Allocations;

[ApiController]
[Route("api/v1")]
public sealed class AllocationHistoryController(IAllocationHistoryService history) : ControllerBase
{
    /// <summary>The full allocation of one settlement (BRD §23) — drill-through target from a ledger line.</summary>
    [HttpGet("settlements/{settlementId:long}/allocations")]
    [HasPermission("vendor_payment_allocation.view")]
    public async Task<ActionResult<SettlementAllocationHistoryDto>> ForSettlement(
        long settlementId, CancellationToken cancellationToken)
    {
        SettlementAllocationHistoryDto? result =
            await history.ForSettlementAsync(settlementId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>BRD §51 Vendor Payment Allocation Report — how each payment was distributed across projects.</summary>
    [HttpGet("reports/vendor-payment-allocations")]
    [HasPermission("vendor_payment_allocation.view")]
    public Task<IReadOnlyList<VendorPaymentAllocationReportRowDto>> Report(
        [FromQuery] long? vendorId,
        [FromQuery] long? projectId,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken cancellationToken) =>
        history.ReportAsync(
            new VendorPaymentAllocationReportQuery(vendorId, projectId, dateFrom, dateTo), cancellationToken);
}
