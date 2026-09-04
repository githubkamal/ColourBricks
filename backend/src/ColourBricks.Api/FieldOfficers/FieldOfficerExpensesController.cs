using ColourBricks.Api.Authorization;
using ColourBricks.Application.FieldOfficers;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.FieldOfficers;

/// <summary>
/// A field officer's no-project bills (client request, 2026-09-04). Gated on
/// <c>vendors.*</c> — field officers are counterparties with a payable exactly like
/// vendors, and there's no BRD-defined module for this client-requested feature.
/// </summary>
[ApiController]
[Route("api/v1/field-officer-expenses")]
public sealed class FieldOfficerExpensesController(IFieldOfficerExpenseService expenses) : ControllerBase
{
    [HttpPost]
    [HasPermission("vendors.add")]
    public async Task<ActionResult<FieldOfficerExpenseDto>> Record(
        [FromBody] RecordFieldOfficerExpenseRequest request, CancellationToken cancellationToken)
    {
        FieldOfficerExpenseDto dto = await expenses.RecordAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), null, dto);
    }

    [HttpGet]
    [HasPermission("vendors.view")]
    public Task<IReadOnlyList<FieldOfficerExpenseDto>> List(
        [FromQuery] long? fieldOfficerId, [FromQuery] string? type,
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo,
        CancellationToken cancellationToken) =>
        expenses.ListAsync(new FieldOfficerExpenseQuery(fieldOfficerId, type, dateFrom, dateTo), cancellationToken);

    [HttpPost("{id:long}/reverse")]
    [HasPermission("vendors.edit")]
    public async Task<IActionResult> Reverse(
        long id, [FromBody] ReverseFieldOfficerExpenseRequest request, CancellationToken cancellationToken)
    {
        bool found = await expenses.ReverseAsync(id, request.Reason, cancellationToken);
        return found ? NoContent() : NotFound();
    }
}

public sealed record ReverseFieldOfficerExpenseRequest(string Reason);
