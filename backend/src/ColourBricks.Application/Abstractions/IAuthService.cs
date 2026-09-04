using ColourBricks.Application.Auth;

namespace ColourBricks.Application.Abstractions;

/// <summary>
/// Authentication use cases (plan.md §9, BRD §59). The API layer owns the HTTP
/// concerns — reading and writing the httpOnly cookies — and delegates the rest here.
/// </summary>
public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken);

    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);

    Task<CurrentUserDto?> GetCurrentUserAsync(long userId, CancellationToken cancellationToken);
}
