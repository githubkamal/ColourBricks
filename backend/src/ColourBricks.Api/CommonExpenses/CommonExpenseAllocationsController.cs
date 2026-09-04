using ColourBricks.Api.Authorization;
using ColourBricks.Application.CommonExpenses;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.CommonExpenses;

[ApiController]
[Route("api/v1/common-expense-allocations")]
public sealed class CommonExpenseAllocationsController(ICommonExpenseAllocationService allocations)
    : ControllerBase
{
    [HttpPost("preview")]
    [HasPermission("common_expenses.view")]
    public Task<CommonExpenseAllocationPreviewDto> Preview(
        [FromBody] CommonExpenseAllocationRequest request, CancellationToken cancellationToken) =>
        allocations.PreviewAsync(request, cancellationToken);

    [HttpPost]
    [HasPermission("common_expenses.add")]
    public async Task<ActionResult<CommonExpenseAllocationRunDto>> Commit(
        [FromBody] CommonExpenseAllocationRequest request, CancellationToken cancellationToken)
    {
        CommonExpenseAllocationRunDto run = await allocations.CommitAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { runId = run.Id }, run);
    }

    [HttpGet]
    [HasPermission("common_expenses.view")]
    public Task<IReadOnlyList<CommonExpenseAllocationRunDto>> List(CancellationToken cancellationToken) =>
        allocations.ListRunsAsync(cancellationToken);

    [HttpGet("{runId:long}")]
    [HasPermission("common_expenses.view")]
    public async Task<ActionResult<CommonExpenseAllocationRunDto>> Get(long runId, CancellationToken cancellationToken)
    {
        CommonExpenseAllocationRunDto? run = await allocations.GetRunAsync(runId, cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpPost("{runId:long}/reverse")]
    [HasPermission("common_expenses.edit")]
    public async Task<IActionResult> Reverse(long runId, CancellationToken cancellationToken)
    {
        await allocations.ReverseRunAsync(runId, cancellationToken);
        return NoContent();
    }

    [HttpGet("/api/v1/reports/common-expense-allocations")]
    [HasPermission("reports.view")]
    public Task<IReadOnlyList<CommonExpenseAllocationReportRowDto>> Report(
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo, [FromQuery] string? type,
        CancellationToken cancellationToken) =>
        allocations.ReportAsync(dateFrom, dateTo, type, cancellationToken);
}
