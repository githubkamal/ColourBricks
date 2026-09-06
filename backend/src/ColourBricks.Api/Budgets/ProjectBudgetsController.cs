using ColourBricks.Api.Authorization;
using ColourBricks.Application.Budgets;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Budgets;

[ApiController]
[Route("api/v1/projects/{projectId:long}/budget")]
public sealed class ProjectBudgetsController(IProjectBudgetService budgets) : ControllerBase
{
    [HttpGet]
    [HasPermission("budget.view")]
    public async Task<IActionResult> GetCurrent(long projectId, CancellationToken cancellationToken)
    {
        // No revision yet is a normal, empty state (not 404) — return it as an
        // explicit 200 with a JSON null body rather than a bare nullable return,
        // whose serialization is otherwise ambiguous between frameworks/clients.
        ProjectBudgetDto? dto = await budgets.GetCurrentAsync(projectId, cancellationToken);
        return Ok(dto);
    }

    [HttpGet("revisions")]
    [HasPermission("budget.view")]
    public Task<IReadOnlyList<BudgetRevisionSummaryDto>> ListRevisions(
        long projectId, CancellationToken cancellationToken) =>
        budgets.ListRevisionsAsync(projectId, cancellationToken);

    [HttpGet("revisions/{revisionNumber:int}")]
    [HasPermission("budget.view")]
    public async Task<ActionResult<ProjectBudgetDto>> GetRevision(
        long projectId, int revisionNumber, CancellationToken cancellationToken)
    {
        ProjectBudgetDto? dto = await budgets.GetRevisionAsync(projectId, revisionNumber, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [HasPermission("budget.edit")]
    public async Task<ActionResult<ProjectBudgetDto>> Save(
        long projectId, [FromBody] SaveProjectBudgetRequest request, CancellationToken cancellationToken)
    {
        ProjectBudgetDto dto = await budgets.SaveRevisionAsync(projectId, request, cancellationToken);
        return CreatedAtAction(nameof(GetRevision),
            new { projectId, revisionNumber = dto.RevisionNumber }, dto);
    }
}
