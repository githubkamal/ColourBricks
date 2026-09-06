using System.Collections.Concurrent;
using System.Text.Json;

namespace ColourBricks.Api.Security;

/// <summary>
/// P9-T03 — brute-force protection on <c>POST /api/v1/auth/login</c>, partitioned by
/// (client IP, submitted email) so repeated attempts against one account are
/// throttled without affecting unrelated logins. Returns 429 once the window is
/// exhausted.
///
/// Only failed attempts (401 — invalid credentials or locked out) count against the
/// limit; a successful login clears it. Counting successes too was a bug: any client
/// that legitimately logs in/out often (the Playwright e2e suite signs in once per
/// spec file, ~28 times in one run) would trip the limiter on volume alone, then
/// keep re-tripping it via Playwright's own retries — every login for the rest of
/// the run failed, with no actual brute-force attempt involved. Found by reproducing
/// the CI e2e failures locally and tracing the network log.
/// </summary>
public sealed class LoginRateLimitMiddleware(RequestDelegate next, TimeProvider clock)
{
    private const int Limit = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private static readonly ConcurrentDictionary<string, (int Count, DateTimeOffset WindowStart)> Attempts = new();

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsLogin(context.Request))
        {
            await next(context);
            return;
        }

        string key = await BuildKeyAsync(context);
        DateTimeOffset now = clock.GetUtcNow();

        if (Attempts.TryGetValue(key, out (int Count, DateTimeOffset WindowStart) existing)
            && now - existing.WindowStart <= Window
            && existing.Count >= Limit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = ((int)Window.TotalSeconds).ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc9457",
                title = "Too many login attempts.",
                status = StatusCodes.Status429TooManyRequests,
            });
            return;
        }

        await next(context);

        if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
        {
            Attempts.AddOrUpdate(
                key,
                _ => (1, now),
                (_, current) => now - current.WindowStart > Window ? (1, now) : (current.Count + 1, current.WindowStart));
        }
        else if (context.Response.StatusCode == StatusCodes.Status200OK)
        {
            Attempts.TryRemove(key, out _);
        }
    }

    private static bool IsLogin(HttpRequest request) =>
        HttpMethods.IsPost(request.Method)
        && request.Path.Equals("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase);

    private static async Task<string> BuildKeyAsync(HttpContext context)
    {
        string ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        string email = "-";

        context.Request.EnableBuffering();
        try
        {
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            string body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(body))
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("email", out JsonElement value) && value.ValueKind == JsonValueKind.String)
                {
                    email = value.GetString()!.Trim().ToLowerInvariant();
                }
            }
        }
        catch (JsonException)
        {
            // Malformed body — fall through with the default email marker.
        }

        return $"{ip}|{email}";
    }
}
