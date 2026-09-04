using ColourBricks.Api.Authorization;
using ColourBricks.Application.PurchaseOrders;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.PurchaseOrders;

/// <summary>
/// Client request (2026-09-04): draft a vendor purchase order across one or more
/// projects, then submit it against the vendor's invoice — gated the same as
/// vendor purchases, since a submitted order becomes one.
/// </summary>
[ApiController]
[Route("api/v1/purchase-orders")]
public sealed class PurchaseOrdersController(IPurchaseOrderService purchaseOrders) : ControllerBase
{
    [HttpPost]
    [HasPermission("materials.add")]
    public async Task<ActionResult<PurchaseOrderDto>> Create(
        [FromBody] CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        PurchaseOrderDto dto = await purchaseOrders.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpGet]
    [HasPermission("materials.view")]
    public Task<IReadOnlyList<PurchaseOrderDto>> List(
        [FromQuery] long? vendorId, [FromQuery] string? status, CancellationToken cancellationToken) =>
        purchaseOrders.ListAsync(vendorId, status, cancellationToken);

    [HttpGet("{id:long}")]
    [HasPermission("materials.view")]
    public async Task<ActionResult<PurchaseOrderDto>> Get(long id, CancellationToken cancellationToken)
    {
        PurchaseOrderDto? dto = await purchaseOrders.GetAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPut("{id:long}")]
    [HasPermission("materials.edit")]
    public async Task<ActionResult<PurchaseOrderDto>> Update(
        long id, [FromBody] UpdatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        PurchaseOrderDto? dto = await purchaseOrders.UpdateAsync(id, request, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("{id:long}/submit")]
    [HasPermission("materials.edit")]
    public Task<PurchaseOrderDto> Submit(
        long id, [FromBody] SubmitPurchaseOrderRequest request, CancellationToken cancellationToken) =>
        purchaseOrders.SubmitAsync(id, request, cancellationToken);

    [HttpPost("{id:long}/cancel")]
    [HasPermission("materials.edit")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        bool done = await purchaseOrders.CancelAsync(id, cancellationToken);
        return done ? NoContent() : NotFound();
    }
}
