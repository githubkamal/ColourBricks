using ColourBricks.Api.Authorization;
using ColourBricks.Application.Reporting;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Reporting;

[ApiController]
[Route("api/v1")]
public sealed class ReportingController(IReportingService reporting) : ControllerBase
{
    [HttpGet("projects/{projectId:long}/budget-vs-actual")]
    [HasPermission("budget.view")]
    public Task<BudgetVsActualDto> BudgetVsActual(long projectId, CancellationToken cancellationToken) =>
        reporting.BudgetVsActualAsync(projectId, cancellationToken);

    [HttpGet("projects/{projectId:long}/financial-ledger")]
    [HasPermission("projects.view")]
    public Task<ProjectLedgerViewDto> ProjectLedger(
        long projectId,
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo,
        [FromQuery] long? categoryId, [FromQuery] long? partyId,
        CancellationToken cancellationToken) =>
        reporting.ProjectLedgerAsync(projectId, dateFrom, dateTo, categoryId, partyId, cancellationToken);

    [HttpGet("projects/{projectId:long}/pnl")]
    [HasPermission("profit_loss.view")]
    public Task<ProjectPnlDto> ProjectPnl(
        long projectId, [FromQuery] string revenueBasis = "Contract",
        CancellationToken cancellationToken = default) =>
        reporting.ProjectPnlAsync(projectId, revenueBasis, cancellationToken);

    [HttpGet("pnl")]
    [HasPermission("profit_loss.view")]
    public Task<CompanyPnlDto> CompanyPnl(
        [FromQuery] string revenueBasis = "Contract", CancellationToken cancellationToken = default) =>
        reporting.CompanyPnlAsync(revenueBasis, cancellationToken);

    [HttpGet("projects/{projectId:long}/dashboard")]
    [HasPermission("dashboard.view")]
    public Task<ProjectDashboardDto> ProjectDashboard(long projectId, CancellationToken cancellationToken) =>
        reporting.ProjectDashboardAsync(projectId, cancellationToken);

    [HttpGet("dashboard")]
    [HasPermission("dashboard.view")]
    public Task<CompanyDashboardDto> CompanyDashboard(CancellationToken cancellationToken) =>
        reporting.CompanyDashboardAsync(cancellationToken);
}
