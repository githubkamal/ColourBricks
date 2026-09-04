using ColourBricks.Api.Authorization;
using ColourBricks.Application.Loans;
using ColourBricks.Application.Settings;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Loans;

[ApiController]
[Route("api/v1/loans")]
public sealed class LoansController(
    ILoanService loans,
    ILoanScheduleService schedule,
    ILoanEmiPaymentService emiPayments,
    ILoanAlertService alerts,
    ISystemSettingsService settings) : ControllerBase
{
    [HttpGet("alerts")]
    [HasPermission("loans.view")]
    public async Task<LoanAlertsDto> Alerts(
        [FromQuery] int? daysAhead, CancellationToken cancellationToken)
    {
        // No explicit override: fall back to the admin-configured System Settings
        // default (was a hardcoded 7 — client-confirmed scope, 2026-09-04).
        int resolved = daysAhead ?? (await settings.GetAsync(cancellationToken)).LoanEmiReminderDaysAhead;
        return await alerts.RunAsync(resolved, cancellationToken);
    }

    [HttpGet("outstanding-summary")]
    [HasPermission("loans.view")]
    public Task<LoanOutstandingSummaryDto> OutstandingSummary(CancellationToken cancellationToken) =>
        alerts.OutstandingSummaryAsync(cancellationToken);

    [HttpPost]
    [HasPermission("loans.add")]
    public async Task<ActionResult<LoanDto>> Record(
        [FromBody] RecordLoanRequest request, CancellationToken cancellationToken)
    {
        LoanDto loan = await loans.RecordAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = loan.Id }, loan);
    }

    [HttpGet]
    [HasPermission("loans.view")]
    public Task<IReadOnlyList<LoanDto>> List(
        [FromQuery] long? projectId, [FromQuery] long? lenderId, CancellationToken cancellationToken) =>
        loans.ListAsync(projectId, lenderId, cancellationToken);

    [HttpGet("{id:long}")]
    [HasPermission("loans.view")]
    public async Task<ActionResult<LoanDto>> Get(long id, CancellationToken cancellationToken)
    {
        LoanDto? loan = await loans.GetAsync(id, cancellationToken);
        return loan is null ? NotFound() : Ok(loan);
    }

    [HttpPost("{id:long}/reverse")]
    [HasPermission("loans.edit")]
    public async Task<IActionResult> Reverse(
        long id, [FromBody] ReverseLoanRequest request, CancellationToken cancellationToken)
    {
        bool done = await loans.ReverseAsync(id, request.Reason, cancellationToken);
        return done ? NoContent() : NotFound();
    }

    [HttpGet("{id:long}/schedule")]
    [HasPermission("emi.view")]
    public Task<IReadOnlyList<LoanEmiInstalmentDto>> Schedule(long id, CancellationToken cancellationToken) =>
        schedule.GetAsync(id, cancellationToken);

    [HttpPost("{id:long}/schedule")]
    [HasPermission("emi.add")]
    public Task<IReadOnlyList<LoanEmiInstalmentDto>> GenerateSchedule(
        long id, [FromBody] GenerateLoanScheduleRequest request, CancellationToken cancellationToken) =>
        schedule.GenerateAsync(id, request, cancellationToken);

    [HttpPost("{id:long}/schedule/regenerate")]
    [HasPermission("emi.edit")]
    public Task<IReadOnlyList<LoanEmiInstalmentDto>> RegenerateSchedule(
        long id, [FromBody] RegenerateLoanScheduleRequest request, CancellationToken cancellationToken) =>
        schedule.RegenerateAsync(id, request, cancellationToken);

    [HttpGet("{id:long}/emi-payments")]
    [HasPermission("emi.view")]
    public Task<IReadOnlyList<LoanEmiPaymentDto>> EmiPayments(long id, CancellationToken cancellationToken) =>
        emiPayments.ListAsync(id, cancellationToken);

    [HttpPost("{id:long}/emi-payments")]
    [HasPermission("emi.add")]
    public Task<LoanEmiPaymentDto> PayEmi(
        long id, [FromBody] PayEmiRequest request, CancellationToken cancellationToken) =>
        emiPayments.PayAsync(id, request, cancellationToken);

    [HttpPost("{id:long}/prepayments")]
    [HasPermission("emi.add")]
    public Task<LoanEmiPaymentDto> Prepay(
        long id, [FromBody] PrepayLoanRequest request, CancellationToken cancellationToken) =>
        emiPayments.PrepayAsync(id, request, cancellationToken);

    [HttpPost("/api/v1/emi-payments/{paymentId:long}/reverse")]
    [HasPermission("emi.edit")]
    public async Task<IActionResult> ReverseEmiPayment(
        long paymentId, [FromBody] ReverseLoanRequest request, CancellationToken cancellationToken)
    {
        bool done = await emiPayments.ReverseAsync(paymentId, request.Reason, cancellationToken);
        return done ? NoContent() : NotFound();
    }
}

public sealed record ReverseLoanRequest(string Reason);
