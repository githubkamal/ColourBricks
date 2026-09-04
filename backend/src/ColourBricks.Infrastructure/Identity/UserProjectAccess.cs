using ColourBricks.Domain.Common;

namespace ColourBricks.Infrastructure.Identity;

/// <summary>
/// An explicit per-project grant (BRD §64). A user with zero rows here is
/// unrestricted; a user with one or more rows sees only those projects.
/// </summary>
/// <remarks>
/// <see cref="ProjectId"/> has no FK yet — the <c>Project</c> entity lands in P1-T01.
/// </remarks>
public sealed class UserProjectAccess : BaseEntity
{
    public long UserId { get; set; }

    public long ProjectId { get; set; }

    public User? User { get; set; }
}
