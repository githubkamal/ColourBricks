using ColourBricks.Api.Authorization;
using ColourBricks.Application.Ledger;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Ledger;

[ApiController]
[Route("api/v1")]
public sealed class LedgerController(
    IExpenseCategoryService categories,
    ILedgerQueryService ledger) : ControllerBase
{
    [HttpGet("expense-categories")]
    [HasPermission("project_expenses.view")]
    public Task<IReadOnlyList<ExpenseCategoryDto>> Categories(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        categories.ListAsync(includeInactive, cancellationToken);

    [HttpGet("projects/{projectId:long}/ledger")]
    [HasPermission("projects.view")]
    public Task<IReadOnlyList<ProjectLedgerRowDto>> ProjectLedger(
        long projectId, CancellationToken cancellationToken) =>
        ledger.GetProjectLedgerAsync(projectId, cancellationToken);

    [HttpGet("projects/{projectId:long}/cost-breakdown")]
    [HasPermission("projects.view")]
    public async Task<IReadOnlyDictionary<string, decimal>> CostBreakdown(
        long projectId, CancellationToken cancellationToken) =>
        await ledger.GetProjectCostByBucketAsync(projectId, cancellationToken);
}
