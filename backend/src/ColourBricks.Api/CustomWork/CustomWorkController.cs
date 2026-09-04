using ColourBricks.Api.Authorization;
using ColourBricks.Application.CustomWork;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.CustomWork;

[ApiController]
[Route("api/v1")]
public sealed class CustomWorkController(
    ICustomWorkService customWork, ICustomWorkPaymentService payments) : ControllerBase
{
    [HttpPost("custom-work")]
    [HasPermission("customized_work.add")]
    public async Task<ActionResult<CustomWorkDto>> Record(
        [FromBody] RecordCustomWorkRequest request, CancellationToken cancellationToken)
    {
        CustomWorkDto work = await customWork.RecordAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { projectId = work.ProjectId }, work);
    }

    [HttpGet("projects/{projectId:long}/custom-work")]
    [HasPermission("customized_work.view")]
    public Task<IReadOnlyList<CustomWorkDto>> List(long projectId, CancellationToken cancellationToken) =>
        customWork.ListAsync(projectId, cancellationToken);

    /// <summary>Reverses the custom-work record itself (client request, 2026-09-04).</summary>
    [HttpPost("custom-work/{id:long}/reverse")]
    [HasPermission("customized_work.edit")]
    public async Task<IActionResult> Reverse(
        long id, [FromBody] ReverseCustomWorkRequest request, CancellationToken cancellationToken)
    {
        bool found = await customWork.ReverseAsync(id, request.Reason, cancellationToken);
        return found ? NoContent() : NotFound();
    }

    /// <summary>Settles a custom-work obligation (client request, 2026-09-04).</summary>
    [HttpPost("custom-work-payments")]
    [HasPermission("customized_work.add")]
    public Task<CustomWorkPaymentDto> Pay(
        [FromBody] RecordCustomWorkPaymentRequest request, CancellationToken cancellationToken) =>
        payments.PayAsync(request, cancellationToken);

    [HttpGet("projects/{projectId:long}/custom-work-payments")]
    [HasPermission("customized_work.view")]
    public Task<IReadOnlyList<CustomWorkPaymentDto>> ListPayments(
        long projectId, [FromQuery] long partyId, CancellationToken cancellationToken) =>
        payments.ListAsync(projectId, partyId, cancellationToken);

    [HttpPost("custom-work-payments/{id:long}/reverse")]
    [HasPermission("customized_work.edit")]
    public async Task<IActionResult> ReversePayment(
        long id, [FromBody] ReverseCustomWorkRequest request, CancellationToken cancellationToken)
    {
        bool found = await payments.ReverseAsync(id, request.Reason, cancellationToken);
        return found ? NoContent() : NotFound();
    }
}

public sealed record ReverseCustomWorkRequest(string Reason);
