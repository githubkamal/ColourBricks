using System.Security.Claims;
using ColourBricks.Application.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ColourBricks.Api.Auth;

/// <summary>
/// Resolves <see cref="ICurrentUser"/> from the authenticated request. Replaces the
/// Infrastructure default so the audit interceptor and <c>IProjectScopeFilter</c>
/// see the real caller.
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public long? UserId
    {
        get
        {
            string? sub = accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return long.TryParse(sub, out long id) ? id : null;
        }
    }
}
