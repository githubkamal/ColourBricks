namespace ColourBricks.Application.Abstractions;

/// <summary>
/// Ambient accessor for the acting user. Implemented in the API layer against
/// <c>HttpContext</c> once authentication lands (P0-T04); until then the default
/// registration resolves to "no user" for system and seed operations.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The acting user's id, or <c>null</c> for unauthenticated/system work.</summary>
    long? UserId { get; }
}
