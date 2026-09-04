using ColourBricks.Api.Authorization;
using ColourBricks.Application.Integrity;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Admin;

[ApiController]
[Route("api/v1/admin/integrity-check")]
public sealed class IntegrityCheckController(IIntegrityCheckService integrity) : ControllerBase
{
    /// <summary>BRD §38 reconciliation controls — 200 with the report whether or not every control passed.</summary>
    [HttpGet]
    [HasPermission("admin_configuration.view")]
    public Task<IntegrityCheckReport> Run(CancellationToken cancellationToken) =>
        integrity.RunAsync(cancellationToken);
}
