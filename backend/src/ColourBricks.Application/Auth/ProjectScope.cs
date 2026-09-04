namespace ColourBricks.Application.Auth;

/// <summary>
/// The set of projects the current user may see (BRD §64). Either unrestricted
/// ("all") or limited to an explicit id set.
/// </summary>
public sealed class ProjectScope
{
    public static readonly ProjectScope All =
        new(isUnrestricted: true, new HashSet<long>());

    private ProjectScope(bool isUnrestricted, IReadOnlySet<long> projectIds)
    {
        IsUnrestricted = isUnrestricted;
        ProjectIds = projectIds;
    }

    public bool IsUnrestricted { get; }

    /// <summary>Allowed project ids. Empty when <see cref="IsUnrestricted"/> is true.</summary>
    public IReadOnlySet<long> ProjectIds { get; }

    public static ProjectScope RestrictedTo(IEnumerable<long> projectIds) =>
        new(isUnrestricted: false, projectIds.ToHashSet());

    public bool Allows(long projectId) => IsUnrestricted || ProjectIds.Contains(projectId);
}
