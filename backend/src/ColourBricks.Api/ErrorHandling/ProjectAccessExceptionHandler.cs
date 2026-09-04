using System.Diagnostics;
using ColourBricks.Application.Reporting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.ErrorHandling;

/// <summary>Turns a <see cref="ProjectAccessDeniedException"/> into a 403 (plan.md §9).</summary>
public sealed class ProjectAccessExceptionHandler(
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ProjectAccessDeniedException denied)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Project outside your access.",
                Detail = denied.Message,
                Type = "https://datatracker.ietf.org/doc/html/rfc9457",
                Extensions = { ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier },
            },
        });
    }
}
