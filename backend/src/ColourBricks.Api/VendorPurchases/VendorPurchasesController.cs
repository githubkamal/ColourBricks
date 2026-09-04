using ColourBricks.Api.Authorization;
using ColourBricks.Application.VendorPurchases;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.VendorPurchases;

[ApiController]
[Route("api/v1")]
public sealed class VendorPurchasesController(IVendorPurchaseService purchases) : ControllerBase
{
    [HttpPost("vendor-purchases")]
    [HasPermission("materials.add")]
    public async Task<IActionResult> Record(
        [FromBody] RecordVendorPurchaseRequest request, CancellationToken cancellationToken)
    {
        RecordVendorPurchaseResult result = await purchases.RecordAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Purchase.Id }, new
        {
            purchase = result.Purchase,
            duplicateInvoiceWarning = result.DuplicateInvoiceWarning,
        });
    }

    [HttpGet("vendor-purchases/{id:long}")]
    [HasPermission("materials.view")]
    public async Task<ActionResult<VendorPurchaseDto>> Get(long id, CancellationToken cancellationToken)
    {
        VendorPurchaseDto? purchase = await purchases.GetAsync(id, cancellationToken);
        return purchase is null ? NotFound() : Ok(purchase);
    }

    [HttpGet("vendor-purchases")]
    [HasPermission("materials.view")]
    public Task<IReadOnlyList<VendorPurchaseDto>> List(
        [FromQuery] long? projectId,
        [FromQuery] long? vendorId,
        CancellationToken cancellationToken = default) =>
        purchases.ListAsync(projectId, vendorId, cancellationToken);

    [HttpGet("vendors/{vendorId:long}/outstanding")]
    [HasPermission("vendors.view")]
    public async Task<ActionResult<object>> VendorOutstanding(
        long vendorId, CancellationToken cancellationToken) =>
        Ok(new { vendorId, outstanding = await purchases.VendorOutstandingAsync(vendorId, cancellationToken) });

    [HttpPost("vendor-purchases/{id:long}/reverse")]
    [HasPermission("materials.edit")]
    public async Task<IActionResult> Reverse(
        long id, [FromBody] ReverseVendorPurchaseRequest request, CancellationToken cancellationToken)
    {
        bool done = await purchases.ReverseAsync(id, request.Reason, cancellationToken);
        return done ? NoContent() : NotFound();
    }
}

public sealed record ReverseVendorPurchaseRequest(string Reason);
