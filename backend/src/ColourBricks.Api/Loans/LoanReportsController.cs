using ColourBricks.Api.Authorization;
using ColourBricks.Application.Loans;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Loans;

/// <summary>P7-T05 — the seven BRD §54 loan reports.</summary>
[ApiController]
[Route("api/v1/reports/loans")]
[HasPermission("reports.view")]
public sealed class LoanReportsController(ILoanReportService reports) : ControllerBase
{
    [HttpGet("project-wise")]
    public Task<IReadOnlyList<ProjectWiseLoanRowDto>> ProjectWise(CancellationToken ct) =>
        reports.ProjectWiseAsync(ct);

    [HttpGet("outstanding")]
    public Task<IReadOnlyList<LoanOutstandingReportRowDto>> Outstanding(
        [FromQuery] long? projectId, CancellationToken ct) =>
        reports.OutstandingAsync(projectId, ct);

    [HttpGet("schedule/{loanId:long}")]
    public Task<IReadOnlyList<LoanEmiInstalmentDto>> Schedule(long loanId, CancellationToken ct) =>
        reports.ScheduleAsync(loanId, ct);

    [HttpGet("emi-paid")]
    public Task<IReadOnlyList<EmiPaidRowDto>> EmiPaid(
        [FromQuery] long? loanId, [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo,
        CancellationToken ct) =>
        reports.EmiPaidAsync(loanId, dateFrom, dateTo, ct);

    [HttpGet("emi-pending")]
    public Task<IReadOnlyList<EmiPendingRowDto>> EmiPending([FromQuery] long? loanId, CancellationToken ct) =>
        reports.EmiPendingAsync(loanId, ct);

    [HttpGet("principal-vs-interest")]
    public Task<IReadOnlyList<PrincipalVsInterestRowDto>> PrincipalVsInterest(
        [FromQuery] long? loanId, CancellationToken ct) =>
        reports.PrincipalVsInterestAsync(loanId, ct);

    [HttpGet("date-wise")]
    public Task<IReadOnlyList<DateWiseEmiRowDto>> DateWise(
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo, CancellationToken ct) =>
        reports.DateWisePaymentsAsync(dateFrom, dateTo, ct);
}
