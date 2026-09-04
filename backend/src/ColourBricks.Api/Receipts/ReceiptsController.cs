using ColourBricks.Api.Authorization;
using ColourBricks.Application.Receipts;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Receipts;

[ApiController]
[Route("api/v1")]
public sealed class ReceiptsController(IReceiptService receipts) : ControllerBase
{
    [HttpPost("receipts")]
    [HasPermission("project_income.add")]
    public async Task<ActionResult<ReceiptDto>> Record(
        [FromBody] RecordReceiptRequest request, CancellationToken cancellationToken)
    {
        ReceiptDto receipt = await receipts.RecordAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = receipt.Id }, receipt);
    }

    [HttpGet("receipts/{id:long}")]
    [HasPermission("project_income.view")]
    public async Task<ActionResult<ReceiptDto>> Get(long id, CancellationToken cancellationToken)
    {
        ReceiptDto? receipt = await receipts.GetAsync(id, cancellationToken);
        return receipt is null ? NotFound() : Ok(receipt);
    }

    [HttpGet("projects/{projectId:long}/receipts")]
    [HasPermission("project_income.view")]
    public Task<IReadOnlyList<ReceiptDto>> List(
        long projectId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken = default) =>
        receipts.ListAsync(projectId, from, to, cancellationToken);

    [HttpGet("projects/{projectId:long}/income-total")]
    [HasPermission("project_income.view")]
    public async Task<ActionResult<object>> IncomeTotal(long projectId, CancellationToken cancellationToken) =>
        Ok(new { projectId, total = await receipts.TotalActiveIncomeAsync(projectId, cancellationToken) });

    [HttpPost("receipts/{id:long}/reverse")]
    [HasPermission("project_income.edit")]
    public async Task<IActionResult> Reverse(
        long id, [FromBody] ReverseReceiptRequest request, CancellationToken cancellationToken)
    {
        bool done = await receipts.ReverseAsync(id, request.Reason, cancellationToken);
        return done ? NoContent() : NotFound();
    }
}
