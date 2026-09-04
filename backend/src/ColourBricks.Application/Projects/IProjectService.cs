using ColourBricks.Application.Common.Pagination;

namespace ColourBricks.Application.Projects;

public interface IProjectService
{
    Task<PagedResult<ProjectListItemDto>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken);

    /// <summary>Every project, all statuses — the read path for reports (BRD §70 rule 31).</summary>
    Task<IReadOnlyList<ProjectListItemDto>> ListForReportingAsync(CancellationToken cancellationToken);

    Task<ProjectDto?> GetAsync(long id, CancellationToken cancellationToken);

    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken);

    Task<ProjectDto?> UpdateAsync(long id, UpdateProjectRequest request, CancellationToken cancellationToken);
}
