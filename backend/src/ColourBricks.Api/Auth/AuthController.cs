using System.Security.Claims;
using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ColourBricks.Api.Auth;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService auth, IOptions<AuthOptions> authOptions) : ControllerBase
{
    private bool CookieSecure => authOptions.Value.CookieSecure;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        AuthResult result = await auth.LoginAsync(request.Email, request.Password, cancellationToken);
        if (!result.Succeeded)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: TitleFor(result.Outcome),
                detail: "Login failed.");
        }

        AuthCookies.Write(Response, result.Tokens!, CookieSecure);
        return Ok(result.User);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        string? refreshToken = AuthCookies.ReadRefreshToken(Request);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Missing refresh token");
        }

        AuthResult result = await auth.RefreshAsync(refreshToken, cancellationToken);
        if (!result.Succeeded)
        {
            AuthCookies.Clear(Response, CookieSecure);
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: TitleFor(result.Outcome));
        }

        AuthCookies.Write(Response, result.Tokens!, CookieSecure);
        return Ok(result.User);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(AuthCookies.ReadRefreshToken(Request), cancellationToken);
        AuthCookies.Clear(Response, CookieSecure);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        string? sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!long.TryParse(sub, out long userId))
        {
            return Unauthorized();
        }

        CurrentUserDto? user = await auth.GetCurrentUserAsync(userId, cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    private static string TitleFor(AuthOutcome outcome) => outcome switch
    {
        AuthOutcome.InactiveUser => "Account is inactive",
        AuthOutcome.LockedOut => "Account is locked",
        AuthOutcome.InvalidRefreshToken => "Invalid or expired session",
        _ => "Invalid credentials",
    };
}

public sealed record LoginRequest(string Email, string Password);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
