using ColourBricks.Api.Authorization;
using ColourBricks.Application.Settings;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Admin;

/// <summary>Company profile + notification-threshold defaults (client-confirmed scope, 2026-09-04).</summary>
[ApiController]
[Route("api/v1/admin/settings")]
public sealed class SystemSettingsController(ISystemSettingsService settings) : ControllerBase
{
    [HttpGet]
    [HasPermission("admin_configuration.view")]
    public Task<SystemSettingsDto> Get(CancellationToken cancellationToken) =>
        settings.GetAsync(cancellationToken);

    [HttpPut]
    [HasPermission("admin_configuration.edit")]
    public Task<SystemSettingsDto> Update(
        [FromBody] UpdateSystemSettingsRequest request, CancellationToken cancellationToken) =>
        settings.UpdateAsync(request, cancellationToken);
}
