namespace ColourBricks.Application.Teams;

public interface ITeamService
{
    Task<IReadOnlyList<TeamDto>> ListAsync(
        long? departmentId, bool includeInactive, CancellationToken cancellationToken);

    /// <summary>The same teams, bucketed by department and ordered for the grouped picker.</summary>
    Task<IReadOnlyList<TeamGroup>> ListGroupedAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<TeamDto?> GetAsync(long id, CancellationToken cancellationToken);

    Task<CreateTeamResult> CreateAsync(
        CreateTeamRequest request, bool confirmed, CancellationToken cancellationToken);

    Task<TeamDto?> UpdateAsync(long id, UpdateTeamRequest request, CancellationToken cancellationToken);
}
