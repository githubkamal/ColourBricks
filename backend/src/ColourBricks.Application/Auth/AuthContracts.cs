namespace ColourBricks.Application.Auth;

public enum AuthOutcome
{
    Success = 0,
    InvalidCredentials = 1,
    InactiveUser = 2,
    LockedOut = 3,
    InvalidRefreshToken = 4,
}

/// <summary>The raw tokens the API layer turns into httpOnly cookies (plan.md §9).</summary>
public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

public sealed record CurrentUserDto(
    long Id,
    string Name,
    string Email,
    string? Mobile,
    long? RoleId,
    long? DepartmentId,
    IReadOnlyList<string> Permissions);

public sealed record AuthResult(AuthOutcome Outcome, AuthTokens? Tokens, CurrentUserDto? User)
{
    public bool Succeeded => Outcome == AuthOutcome.Success;

    public static AuthResult Fail(AuthOutcome outcome) => new(outcome, null, null);

    public static AuthResult Success(AuthTokens tokens, CurrentUserDto user) =>
        new(AuthOutcome.Success, tokens, user);
}
