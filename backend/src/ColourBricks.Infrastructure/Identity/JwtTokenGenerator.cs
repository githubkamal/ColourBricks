using System.Security.Claims;
using System.Text;
using ColourBricks.Application.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ColourBricks.Infrastructure.Identity;

/// <summary>Mints short-lived HS256 access tokens for authenticated users.</summary>
public sealed class JwtTokenGenerator(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    private readonly JwtOptions _options = options.Value;

    public const string PermissionsClaim = "permissions";
    public const string AllPermissions = "*";

    public (string Token, DateTimeOffset ExpiresAtUtc) CreateAccessToken(
        User user,
        IReadOnlyCollection<string> permissions,
        bool grantsEveryPermission)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.Name),
        };

        if (user.RoleId is { } roleId)
        {
            claims.Add(new Claim("role_id", roleId.ToString()));
        }

        // Flatten the user's permissions into one space-delimited claim (plan.md §9).
        // Full-access roles collapse to "*" so the token never blows the cookie limit.
        if (grantsEveryPermission)
        {
            claims.Add(new Claim(PermissionsClaim, AllPermissions));
        }
        else if (permissions.Count > 0)
        {
            claims.Add(new Claim(PermissionsClaim, string.Join(' ', permissions)));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
