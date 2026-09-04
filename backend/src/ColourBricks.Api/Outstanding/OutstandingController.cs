using ColourBricks.Api.Authorization;
using ColourBricks.Application.Outstanding;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Outstanding;

[ApiController]
[Route("api/v1")]
public sealed class OutstandingController(IOutstandingService outstanding) : ControllerBase
{
    [HttpGet("vendors/{vendorId:long}/outstanding-summary")]
    [HasPermission("vendors.view")]
    public Task<VendorOutstandingSummaryDto> VendorSummary(
        long vendorId, CancellationToken cancellationToken) =>
        outstanding.VendorSummaryAsync(vendorId, cancellationToken);

    [HttpGet("vendors/{vendorId:long}/ageing")]
    [HasPermission("vendors.view")]
    public Task<AgeingBucketsDto> VendorAgeing(long vendorId, CancellationToken cancellationToken) =>
        outstanding.VendorAgeingAsync(vendorId, cancellationToken);

    [HttpGet("subcontractors/{teamId:long}/outstanding")]
    [HasPermission("labour.view")]
    public async Task<ActionResult<object>> SubcontractorOutstanding(
        long teamId, CancellationToken cancellationToken) =>
        Ok(new { teamId, outstanding = await outstanding.SubcontractorTotalAsync(teamId, cancellationToken) });

    [HttpGet("projects/{projectId:long}/outstanding-summary")]
    [HasPermission("projects.view")]
    public Task<ProjectOutstandingSummaryDto> ProjectSummary(
        long projectId, CancellationToken cancellationToken) =>
        outstanding.ProjectSummaryAsync(projectId, cancellationToken);

    [HttpGet("projects/{projectId:long}/outstanding-by-vendor")]
    [HasPermission("projects.view")]
    public Task<IReadOnlyList<PartyOutstandingLineDto>> ProjectByVendor(
        long projectId, CancellationToken cancellationToken) =>
        outstanding.ProjectByVendorAsync(projectId, cancellationToken);
}
