using ColourBricks.Api.Authorization;
using ColourBricks.Application.Common.Pagination;
using ColourBricks.Application.Projects;
using ColourBricks.Domain.Projects;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Projects;

[ApiController]
[Route("api/v1/projects")]
public sealed class ProjectsController(IProjectService projects) : ControllerBase
{
    [HttpGet]
    [HasPermission("projects.view")]
    public Task<PagedResult<ProjectListItemDto>> List(
        [FromQuery] ProjectStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken cancellationToken = default) =>
        projects.ListAsync(new ProjectListQuery(status, search, page, pageSize, sortBy, sortDir), cancellationToken);

    [HttpGet("reporting")]
    [HasPermission("projects.view")]
    public Task<IReadOnlyList<ProjectListItemDto>> ListForReporting(CancellationToken cancellationToken) =>
        projects.ListForReportingAsync(cancellationToken);

    [HttpGet("{id:long}")]
    [HasPermission("projects.view")]
    public async Task<ActionResult<ProjectDto>> Get(long id, CancellationToken cancellationToken)
    {
        ProjectDto? project = await projects.GetAsync(id, cancellationToken);
        return project is null ? NotFound() : Ok(project);
    }

    [HttpPost]
    [HasPermission("projects.add")]
    public async Task<ActionResult<ProjectDto>> Create(
        [FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        ProjectDto created = await projects.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [HasPermission("projects.edit")]
    public async Task<ActionResult<ProjectDto>> Update(
        long id, [FromBody] UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        ProjectDto? updated = await projects.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:long}")]
    [HasPermission("projects.delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken) =>
        await projects.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
