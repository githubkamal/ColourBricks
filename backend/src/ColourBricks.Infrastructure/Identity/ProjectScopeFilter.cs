using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Auth;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Identity;

/// <summary>
/// A user with no <see cref="UserProjectAccess"/> rows is unrestricted; a user with
/// one or more rows sees only those projects (BRD §64). System/unauthenticated
/// contexts are unrestricted.
/// </summary>
public sealed class ProjectScopeFilter(AppDbContext db, ICurrentUser currentUser) : IProjectScopeFilter
{
    public async ValueTask<ProjectScope> GetScopeAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return ProjectScope.All;
        }

        List<long> projectIds = await db.UserProjectAccess
            .Where(a => a.UserId == userId)
            .Select(a => a.ProjectId)
            .ToListAsync(cancellationToken);

        return projectIds.Count == 0
            ? ProjectScope.All
            : ProjectScope.RestrictedTo(projectIds);
    }
}
