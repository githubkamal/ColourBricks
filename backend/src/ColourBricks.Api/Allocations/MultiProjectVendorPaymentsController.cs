using System.Diagnostics;
using System.Security.Claims;
using ColourBricks.Api.Authorization;
using ColourBricks.Application.Allocations;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Allocations;

[ApiController]
[Route("api/v1/vendor-payments")]
public sealed class MultiProjectVendorPaymentsController(
    IMultiProjectVendorPaymentService payments) : ControllerBase
{
    [HttpPost("propose")]
    [HasPermission("vendor_payment_allocation.view")]
    public Task<AllocationProposalDto> Propose(
        [FromBody] ProposeAllocationRequest request, CancellationToken cancellationToken) =>
        payments.ProposeAsync(request.VendorId, request.Amount, cancellationToken);

    [HttpPost("allocate")]
    [HasPermission("vendor_payment_allocation.add")]
    public async Task<IActionResult> Allocate(
        [FromBody] RecordMultiProjectPaymentRequest request, CancellationToken cancellationToken)
    {
        // Overriding the FIFO proposal (supplying explicit allocations) is Accounts/Admin only.
        if (request.Allocations is { Count: > 0 } && !HasPermission("vendor_payment_allocation.approve"))
        {
            return Forbid();
        }

        try
        {
            MultiProjectPaymentDto result = await payments.PayAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (AllocationExceedsOutstandingException ex)
        {
            return Problem409(ex.Message);
        }
        catch (AllocationSumMismatchException ex)
        {
            return Problem400(ex.Message);
        }
    }

    private bool HasPermission(string permission)
    {
        string? claim = User.FindFirstValue("permissions");
        return claim is not null
            && (claim == "*" || claim.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(permission));
    }

    private ObjectResult Problem400(string detail) =>
        new(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The allocation is invalid.",
            Detail = detail,
            Type = "https://datatracker.ietf.org/doc/html/rfc9457",
            Extensions = { ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier },
        })
        { StatusCode = StatusCodes.Status400BadRequest, ContentTypes = { "application/problem+json" } };

    private ObjectResult Problem409(string detail) =>
        new(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "The allocation could not be applied.",
            Detail = detail,
            Type = "https://datatracker.ietf.org/doc/html/rfc9457",
            Extensions = { ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier },
        })
        { StatusCode = StatusCodes.Status409Conflict, ContentTypes = { "application/problem+json" } };
}

public sealed record ProposeAllocationRequest(long VendorId, decimal Amount);
