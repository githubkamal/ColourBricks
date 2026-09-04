using ColourBricks.Api.Authorization;
using ColourBricks.Application.CommonExpenses;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.CommonExpenses;

[ApiController]
[Route("api/v1/common-expenses")]
public sealed class CommonExpensesController(ICommonExpenseService expenses) : ControllerBase
{
    [HttpPost]
    [HasPermission("common_expenses.add")]
    public async Task<ActionResult<CommonExpenseDto>> Record(
        [FromBody] RecordCommonExpenseRequest request, CancellationToken cancellationToken)
    {
        CommonExpenseDto dto = await expenses.RecordAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), null, dto);
    }

    [HttpGet]
    [HasPermission("common_expenses.view")]
    public Task<IReadOnlyList<CommonExpenseDto>> List(
        [FromQuery] string? type, [FromQuery] string? subCategory,
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo,
        [FromQuery] long? paymentModeId, [FromQuery] long? accountId,
        CancellationToken cancellationToken) =>
        expenses.ListAsync(
            new CommonExpenseQuery(type, subCategory, dateFrom, dateTo, paymentModeId, accountId),
            cancellationToken);

    [HttpGet("summary")]
    [HasPermission("common_expenses.view")]
    public Task<CommonExpenseSummaryDto> Summary(
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo, CancellationToken cancellationToken) =>
        expenses.SummaryAsync(dateFrom, dateTo, cancellationToken);
}
