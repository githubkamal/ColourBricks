using ColourBricks.Application.Auth;

namespace ColourBricks.Api.Auth;

/// <summary>
/// Reads and writes the authentication cookies. Both are httpOnly + SameSite=Lax so
/// the SPA never touches the tokens in JS (plan.md §8.3, §9). Both also use Path=/ so
/// the frontend's Next.js middleware can see them (with a shared Auth:CookieDomain) to
/// gate anonymous requests server-side instead of only after a client-side API call.
/// </summary>
public static class AuthCookies
{
    public const string AccessTokenCookie = "cb_access";
    public const string RefreshTokenCookie = "cb_refresh";

    public static string? ReadRefreshToken(HttpRequest request) =>
        request.Cookies.TryGetValue(RefreshTokenCookie, out string? value) ? value : null;

    public static void Write(HttpResponse response, AuthTokens tokens, bool secure, string? domain)
    {
        response.Cookies.Append(AccessTokenCookie, tokens.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = "/",
            Domain = domain,
            Expires = tokens.AccessTokenExpiresAtUtc,
        });

        response.Cookies.Append(RefreshTokenCookie, tokens.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = "/",
            Domain = domain,
            Expires = tokens.RefreshTokenExpiresAtUtc,
        });
    }

    public static void Clear(HttpResponse response, bool secure, string? domain)
    {
        response.Cookies.Append(AccessTokenCookie, string.Empty, Expired(secure, "/", domain));
        response.Cookies.Append(RefreshTokenCookie, string.Empty, Expired(secure, "/", domain));
    }

    private static CookieOptions Expired(bool secure, string path, string? domain) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = path,
        Domain = domain,
        Expires = DateTimeOffset.UnixEpoch,
    };
}
