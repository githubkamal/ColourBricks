using ColourBricks.Api.Authorization;
using ColourBricks.Application.VendorPayments;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.VendorPayments;

[ApiController]
[Route("api/v1")]
public sealed class VendorPaymentsController(IVendorPaymentService payments) : ControllerBase
{
    [HttpPost("vendor-payments")]
    [HasPermission("payments.add")]
    public async Task<ActionResult<VendorPaymentDto>> Pay(
        [FromBody] RecordVendorPaymentRequest request, CancellationToken cancellationToken)
    {
        VendorPaymentDto payment = await payments.PayAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { vendorId = payment.VendorId }, payment);
    }

    [HttpPost("vendor-payments/apply-advance")]
    [HasPermission("payments.add")]
    public Task<ApplyVendorAdvanceDto> ApplyAdvance(
        [FromBody] ApplyVendorAdvanceRequest request, CancellationToken cancellationToken) =>
        payments.ApplyAdvanceAsync(request, cancellationToken);

    [HttpGet("vendors/{vendorId:long}/payments")]
    [HasPermission("payments.view")]
    public Task<IReadOnlyList<VendorPaymentDto>> List(
        long vendorId,
        [FromQuery] long? projectId,
        CancellationToken cancellationToken = default) =>
        payments.ListForVendorAsync(vendorId, projectId, cancellationToken);

    [HttpGet("vendors/{vendorId:long}/statement")]
    [HasPermission("payments.view")]
    public Task<IReadOnlyList<VendorStatementRowDto>> Statement(
        long vendorId, CancellationToken cancellationToken) =>
        payments.StatementAsync(vendorId, cancellationToken);

    [HttpPost("vendor-payments/{id:long}/reverse")]
    [HasPermission("payments.edit")]
    public async Task<IActionResult> Reverse(
        long id, [FromBody] ReverseVendorPaymentRequest request, CancellationToken cancellationToken)
    {
        bool done = await payments.ReverseAsync(id, request.Reason, cancellationToken);
        return done ? NoContent() : NotFound();
    }
}

public sealed record ReverseVendorPaymentRequest(string Reason);
