namespace ColourBricks.Application.Auth;

/// <summary>Bound from the <c>Jwt</c> configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "colourbricks";
    public string Audience { get; init; } = "colourbricks";
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
}

/// <summary>Bound from the <c>Auth</c> configuration section.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public int RefreshTokenDays { get; init; } = 14;
    public int MaxFailedAttempts { get; init; } = 5;
    public int LockoutMinutes { get; init; } = 15;

    /// <summary>Set <c>Secure=false</c> only for plain-HTTP local testing.</summary>
    public bool CookieSecure { get; init; } = true;

    public SeedAdministrator Seed { get; init; } = new();

    public sealed class SeedAdministrator
    {
        public string? Email { get; init; }
        public string? Name { get; init; }
        public string? Password { get; init; }
        public long? RoleId { get; init; }
    }
}
