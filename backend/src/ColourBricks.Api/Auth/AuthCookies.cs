using ColourBricks.Application.Auth;

namespace ColourBricks.Api.Auth;

/// <summary>
/// Reads and writes the authentication cookies. Both are httpOnly + SameSite=Lax so
/// the SPA never touches the tokens in JS (plan.md §8.3, §9).
/// </summary>
public static class AuthCookies
{
    public const string AccessTokenCookie = "cb_access";
    public const string RefreshTokenCookie = "cb_refresh";

    // The refresh cookie is only ever sent to the auth endpoints.
    private const string RefreshPath = "/api/v1/auth";

    public static string? ReadRefreshToken(HttpRequest request) =>
        request.Cookies.TryGetValue(RefreshTokenCookie, out string? value) ? value : null;

    public static void Write(HttpResponse response, AuthTokens tokens, bool secure)
    {
        response.Cookies.Append(AccessTokenCookie, tokens.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = "/",
            Expires = tokens.AccessTokenExpiresAtUtc,
        });

        response.Cookies.Append(RefreshTokenCookie, tokens.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = RefreshPath,
            Expires = tokens.RefreshTokenExpiresAtUtc,
        });
    }

    public static void Clear(HttpResponse response, bool secure)
    {
        response.Cookies.Append(AccessTokenCookie, string.Empty, Expired(secure, "/"));
        response.Cookies.Append(RefreshTokenCookie, string.Empty, Expired(secure, RefreshPath));
    }

    private static CookieOptions Expired(bool secure, string path) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = path,
        Expires = DateTimeOffset.UnixEpoch,
    };
}
