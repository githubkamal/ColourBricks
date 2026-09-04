using System.Diagnostics;
using ColourBricks.Api.Authorization;
using ColourBricks.Application.Departments;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Departments;

[ApiController]
[Route("api/v1/departments")]
public sealed class DepartmentsController(IDepartmentService departments) : ControllerBase
{
    [HttpGet]
    [HasPermission("labour.view")]
    public Task<IReadOnlyList<DepartmentDto>> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        departments.ListAsync(includeInactive, cancellationToken);

    [HttpGet("{id:long}")]
    [HasPermission("labour.view")]
    public async Task<ActionResult<DepartmentDto>> Get(long id, CancellationToken cancellationToken)
    {
        DepartmentDto? department = await departments.GetAsync(id, cancellationToken);
        return department is null ? NotFound() : Ok(department);
    }

    [HttpPost]
    [HasPermission("labour.add")]
    public async Task<IActionResult> Create(
        [FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            DepartmentDto department = await departments.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = department.Id }, department);
        }
        catch (DepartmentExactDuplicateException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    [HttpPut("{id:long}")]
    [HasPermission("labour.edit")]
    public async Task<ActionResult<DepartmentDto>> Update(
        long id, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            DepartmentDto? updated = await departments.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (DepartmentExactDuplicateException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    private ObjectResult Conflict409(string detail, long existingId)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "A department with this name already exists.",
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
