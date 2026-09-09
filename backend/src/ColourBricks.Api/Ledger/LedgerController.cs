using System.Diagnostics;
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

    [HttpPost("expense-categories")]
    [HasPermission("project_expenses.add")]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateExpenseCategoryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ExpenseCategoryDto category = await categories.CreateAsync(request, cancellationToken);
            return Created($"/api/v1/expense-categories/{category.Id}", category);
        }
        catch (ExpenseCategoryExactDuplicateException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    [HttpPut("expense-categories/{id:long}")]
    [HasPermission("project_expenses.edit")]
    public async Task<IActionResult> UpdateCategory(
        long id, [FromBody] UpdateExpenseCategoryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ExpenseCategoryDto? updated = await categories.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ExpenseCategoryExactDuplicateException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
        catch (ExpenseCategoryIsSystemException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "This category is system-owned.",
                Detail = ex.Message,
            });
        }
    }

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

    private ObjectResult Conflict409(string detail, long existingId)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "A record with this name already exists.",
            Detail = detail,
            Type = "https://datatracker.ietf.org/doc/html/rfc9457",
            Extensions =
            {
                ["existingId"] = existingId,
                ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            },
        };
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status409Conflict,
            ContentTypes = { "application/problem+json" },
        };
    }
}
