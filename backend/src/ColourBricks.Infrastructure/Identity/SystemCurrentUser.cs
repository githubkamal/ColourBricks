using ColourBricks.Application.Abstractions;

namespace ColourBricks.Infrastructure.Identity;

/// <summary>
/// Default <see cref="ICurrentUser"/> used before authentication exists (P0-T04)
/// and for background/seed work: there is no acting user.
/// </summary>
public sealed class SystemCurrentUser : ICurrentUser
{
    public long? UserId => null;
}
