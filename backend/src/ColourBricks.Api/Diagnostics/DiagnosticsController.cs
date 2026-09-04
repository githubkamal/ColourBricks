using ColourBricks.Api.Authorization;
using ColourBricks.Api.Idempotency;
using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Diagnostics;

/// <summary>
/// Endpoints that exist only to exercise the cross-cutting pipeline from P0-T03
/// (exception handling, validation, idempotency). Available when the environment is
/// Development or <c>Diagnostics:Enabled</c> is true; otherwise every route is 404.
/// </summary>
[ApiController]
[Route("api/v1/_diagnostics")]
public sealed class DiagnosticsController(
    IHostEnvironment environment,
    IConfiguration configuration,
    DiagnosticWriteCounter counter) : ControllerBase
{
    private bool Enabled =>
        environment.IsDevelopment() || configuration.GetValue<bool>("Diagnostics:Enabled");

    [HttpGet("throw")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Throw() =>
        Enabled
            ? throw new InvalidOperationException("Deliberate diagnostic failure.")
            : NotFound();

    [HttpPost("validate")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Validate([FromBody] DiagnosticPayload payload) =>
        Enabled ? Ok(payload) : NotFound();

    [HttpPost("idempotent-write")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [Idempotent]
    public IActionResult IdempotentWrite() =>
        Enabled ? Ok(new { count = counter.Increment(), id = Guid.NewGuid() }) : NotFound();

    /// <summary>Requires the <c>reports.view</c> permission — exercises P0-T05 authz.</summary>
    [HttpGet("secure-view")]
    [HasPermission("reports.view")]
    public IActionResult SecureView() => Enabled ? Ok(new { ok = true }) : NotFound();

    [HttpGet("project-scope")]
    [Authorize]
    public async Task<IActionResult> ProjectScopeInfo(
        [FromServices] IProjectScopeFilter scopeFilter,
        CancellationToken cancellationToken)
    {
        if (!Enabled)
        {
            return NotFound();
        }

        ProjectScope scope = await scopeFilter.GetScopeAsync(cancellationToken);
        return Ok(new
        {
            unrestricted = scope.IsUnrestricted,
            projectIds = scope.ProjectIds.OrderBy(id => id).ToArray(),
        });
    }
}

/// <summary>Body for <c>POST /api/v1/_diagnostics/validate</c>.</summary>
public sealed record DiagnosticPayload(string Name, int Quantity);

public sealed class DiagnosticPayloadValidator : AbstractValidator<DiagnosticPayload>
{
    public DiagnosticPayloadValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
