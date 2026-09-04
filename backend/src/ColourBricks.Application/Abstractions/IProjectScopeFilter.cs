using ColourBricks.Application.Auth;

namespace ColourBricks.Application.Abstractions;

/// <summary>
/// Resolves the current user's project scope once, in the data layer, so query
/// handlers can append <c>WHERE ProjectId IN (...)</c> for restricted users
/// (plan.md §9). One place to get right, one place to test.
/// </summary>
public interface IProjectScopeFilter
{
    ValueTask<ProjectScope> GetScopeAsync(CancellationToken cancellationToken);
}
