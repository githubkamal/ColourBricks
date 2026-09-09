using ColourBricks.Api.Authorization;
using ColourBricks.Application.DirectExpenses;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.DirectExpenses;

[ApiController]
[Route("api/v1")]
public sealed class DirectExpensesController(IDirectExpenseService expenses) : ControllerBase
{
    [HttpPost("project-expenses")]
    [HasPermission("project_expenses.add")]
    public async Task<ActionResult<DirectExpenseDto>> Record(
        [FromBody] RecordDirectExpenseRequest request, CancellationToken cancellationToken)
    {
        DirectExpenseDto expense = await expenses.RecordAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { projectId = expense.ProjectId }, expense);
    }

    [HttpGet("projects/{projectId:long}/expenses")]
    [HasPermission("project_expenses.view")]
    public Task<IReadOnlyList<DirectExpenseDto>> List(long projectId, CancellationToken cancellationToken) =>
        expenses.ListAsync(projectId, cancellationToken);

    [HttpPost("project-expenses/{id:long}/pay")]
    [HasPermission("project_expenses.edit")]
    public async Task<ActionResult<DirectExpenseDto>> Pay(
        long id, [FromBody] PayDirectExpenseRequest request, CancellationToken cancellationToken)
    {
        DirectExpenseDto? expense = await expenses.PayAsync(id, request, cancellationToken);
        return expense is null ? NotFound() : Ok(expense);
    }

    [HttpPost("project-expenses/{id:long}/reverse")]
    [HasPermission("project_expenses.edit")]
    public async Task<IActionResult> Reverse(
        long id, [FromBody] ReverseDirectExpenseRequest request, CancellationToken cancellationToken)
    {
        bool found = await expenses.ReverseAsync(id, request.Reason, cancellationToken);
        return found ? NoContent() : NotFound();
    }
}

public sealed record ReverseDirectExpenseRequest(string Reason);
