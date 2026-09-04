using System.Diagnostics;
using ColourBricks.Infrastructure.Idempotency;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Api.Idempotency;

/// <summary>
/// Enforces <c>Idempotency-Key</c> on endpoints marked <see cref="IdempotentAttribute"/>
/// (plan.md §7). The first request runs; its response is stored. A repeat with the
/// same key inside 24 hours replays the stored response without touching the handler.
/// </summary>
public sealed class IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
{
    public const string HeaderName = "Idempotency-Key";
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IdempotentAttribute>() is null)
        {
            await next(context);
            return;
        }

        if (!TryGetKey(context, out string key))
        {
            await WriteProblem(context, StatusCodes.Status400BadRequest,
                "Missing Idempotency-Key", $"This endpoint requires a non-empty '{HeaderName}' header.");
            return;
        }

        AppDbContext db = context.RequestServices.GetRequiredService<AppDbContext>();
        TimeProvider clock = context.RequestServices.GetRequiredService<TimeProvider>();
        DateTimeOffset cutoff = clock.GetUtcNow() - Window;

        IdempotencyRecord? existing = await db.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == key && r.CreatedAtUtc > cutoff, context.RequestAborted);

        if (existing is not null)
        {
            await Replay(context, existing);
            return;
        }

        IdempotencyRecord record = new()
        {
            Key = key,
            Method = context.Request.Method,
            Path = context.Request.Path.Value ?? string.Empty,
        };
        db.IdempotencyRecords.Add(record);

        try
        {
            await db.SaveChangesAsync(context.RequestAborted);
        }
        catch (DbUpdateException)
        {
            // Lost the race to reserve the key — another request holds it.
            await WriteProblem(context, StatusCodes.Status409Conflict,
                "Request in progress",
                "A request with this Idempotency-Key is already being processed.");
            return;
        }

        (int statusCode, string? contentType, string body) = await RunAndCapture(context);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            // Do not cache server errors; let the client retry the same key.
            db.IdempotencyRecords.Remove(record);
            await db.SaveChangesAsync(CancellationToken.None);
            return;
        }

        record.StatusCode = statusCode;
        record.ContentType = contentType;
        record.ResponseBody = body;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static bool TryGetKey(HttpContext context, out string key)
    {
        key = context.Request.Headers[HeaderName].ToString().Trim();
        return key.Length > 0;
    }

    private async Task<(int StatusCode, string? ContentType, string Body)> RunAndCapture(HttpContext context)
    {
        Stream originalBody = context.Response.Body;
        await using MemoryStream buffer = new();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        buffer.Position = 0;
        string body = await new StreamReader(buffer).ReadToEndAsync(context.RequestAborted);
        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody, context.RequestAborted);

        return (context.Response.StatusCode, context.Response.ContentType, body);
    }

    private async Task Replay(HttpContext context, IdempotencyRecord record)
    {
        if (record.StatusCode is null)
        {
            await WriteProblem(context, StatusCodes.Status409Conflict,
                "Request in progress",
                "A request with this Idempotency-Key is already being processed.");
            return;
        }

        logger.LogInformation(
            "Replaying stored response for Idempotency-Key {Key} ({StatusCode}).",
            record.Key, record.StatusCode);

        context.Response.StatusCode = record.StatusCode.Value;
        if (record.ContentType is not null)
        {
            context.Response.ContentType = record.ContentType;
        }

        context.Response.Headers["Idempotency-Replayed"] = "true";

        if (!string.IsNullOrEmpty(record.ResponseBody))
        {
            await context.Response.WriteAsync(record.ResponseBody, context.RequestAborted);
        }
    }

    private static async Task WriteProblem(HttpContext context, int status, string title, string detail)
    {
        IProblemDetailsService problemDetails =
            context.RequestServices.GetRequiredService<IProblemDetailsService>();

        context.Response.StatusCode = status;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = status,
                Title = title,
                Detail = detail,
                Extensions = { ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier },
            },
        });
    }
}
