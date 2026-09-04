using System.Diagnostics;
using ColourBricks.Api.Authorization;
using ColourBricks.Application.Payments;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Payments;

[ApiController]
[Route("api/v1/payment-modes")]
public sealed class PaymentModesController(IPaymentModeService modes) : ControllerBase
{
    [HttpGet]
    [HasPermission("accounts.view")]
    public Task<IReadOnlyList<PaymentModeDto>> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        modes.ListAsync(includeInactive, cancellationToken);

    [HttpGet("{id:long}")]
    [HasPermission("accounts.view")]
    public async Task<ActionResult<PaymentModeDto>> Get(long id, CancellationToken cancellationToken)
    {
        PaymentModeDto? mode = await modes.GetAsync(id, cancellationToken);
        return mode is null ? NotFound() : Ok(mode);
    }

    [HttpPost]
    [HasPermission("accounts.add")]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentModeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            PaymentModeDto mode = await modes.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = mode.Id }, mode);
        }
        catch (PaymentModeExactDuplicateException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    [HttpPut("{id:long}")]
    [HasPermission("accounts.edit")]
    public async Task<ActionResult<PaymentModeDto>> Update(
        long id, [FromBody] UpdatePaymentModeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            PaymentModeDto? updated = await modes.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (PaymentModeExactDuplicateException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    /// <summary>
    /// Validates a <see cref="PaymentInstruction"/> against the chosen mode's flags
    /// (BRD §28). Settlement commands call the same service method from P2 onward;
    /// this endpoint lets the entry form check before submit. 204 = valid.
    /// </summary>
    [HttpPost("validate")]
    [HasPermission("accounts.view")]
    public async Task<IActionResult> Validate(
        [FromBody] PaymentInstruction instruction, CancellationToken cancellationToken)
    {
        await modes.ValidateInstructionAsync(instruction, cancellationToken);
        return NoContent();
    }

    private ObjectResult Conflict409(string detail, long existingId)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "A payment mode with this name already exists.",
            Detail = detail,
            Type = "https://datatracker.ietf.org/doc/html/rfc9457",
            Extensions =
            {
                ["existingId"] = existingId,
                ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            },
        };
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status409Conflict,
            ContentTypes = { "application/problem+json" },
        };
    }
}
