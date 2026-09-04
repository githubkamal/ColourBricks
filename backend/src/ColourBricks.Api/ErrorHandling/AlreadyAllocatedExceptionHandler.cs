using System.Diagnostics;
using ColourBricks.Application.CommonExpenses;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.ErrorHandling;

/// <summary>Turns an <see cref="AlreadyAllocatedException"/> into a 409 (BRD §47, rule 42).</summary>
public sealed class AlreadyAllocatedExceptionHandler(
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AlreadyAllocatedException ex)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Already allocated.",
                Detail = ex.Message,
                Type = "https://datatracker.ietf.org/doc/html/rfc9457",
                Extensions = { ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier },
            },
        });
    }
}
