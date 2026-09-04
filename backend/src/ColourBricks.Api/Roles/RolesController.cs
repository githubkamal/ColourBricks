using System.Diagnostics;
using ColourBricks.Api.Authorization;
using ColourBricks.Application.Roles;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Roles;

[ApiController]
[Route("api/v1/roles")]
public sealed class RolesController(IRoleAdminService roles) : ControllerBase
{
    [HttpGet]
    [HasPermission("roles.view")]
    public Task<IReadOnlyList<RoleSummaryDto>> List(CancellationToken cancellationToken) =>
        roles.ListAsync(cancellationToken);

    [HttpGet("catalogue")]
    [HasPermission("roles.view")]
    public Task<PermissionCatalogueDto> Catalogue(CancellationToken cancellationToken) =>
        roles.GetCatalogueAsync(cancellationToken);

    [HttpGet("{id:long}")]
    [HasPermission("roles.view")]
    public async Task<ActionResult<RoleDetailDto>> Get(long id, CancellationToken cancellationToken)
    {
        RoleDetailDto? role = await roles.GetAsync(id, cancellationToken);
        return role is null ? NotFound() : Ok(role);
    }

    [HttpPost]
    [HasPermission("roles.add")]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            RoleDetailDto role = await roles.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = role.Id }, role);
        }
        catch (RoleNameInUseException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    [HttpPut("{id:long}")]
    [HasPermission("roles.edit")]
    public async Task<ActionResult<RoleDetailDto>> Update(
        long id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            RoleDetailDto? role = await roles.UpdateAsync(id, request, cancellationToken);
            return role is null ? NotFound() : Ok(role);
        }
        catch (RoleNameInUseException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
        catch (SystemRoleImmutableException ex)
        {
            return Problem400(ex.Message);
        }
    }

    [HttpPut("{id:long}/permissions")]
    [HasPermission("permissions.edit")]
    public async Task<ActionResult<RoleDetailDto>> SetPermissions(
        long id, [FromBody] SetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            RoleDetailDto? role = await roles.SetPermissionsAsync(id, request, cancellationToken);
            return role is null ? NotFound() : Ok(role);
        }
        catch (AdministratorCorePermissionsException ex)
        {
            return Problem400(ex.Message);
        }
    }

    [HttpDelete("{id:long}")]
    [HasPermission("roles.delete")]
    public async Task<IActionResult> Delete(
        long id, [FromQuery] string reason, CancellationToken cancellationToken)
    {
        try
        {
            return await roles.DeleteAsync(id, reason, cancellationToken) ? NoContent() : NotFound();
        }
        catch (SystemRoleImmutableException ex)
        {
            return Problem400(ex.Message);
        }
    }

    private ObjectResult Problem400(string detail) =>
        new(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The request could not be completed.",
            Detail = detail,
            Type = "https://datatracker.ietf.org/doc/html/rfc9457",
            Extensions = { ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier },
        })
        { StatusCode = StatusCodes.Status400BadRequest, ContentTypes = { "application/problem+json" } };

    private ObjectResult Conflict409(string detail, long existingId) =>
        new(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "A role with this name already exists.",
            Detail = detail,
            Type = "https://datatracker.ietf.org/doc/html/rfc9457",
            Extensions =
            {
                ["existingId"] = existingId,
                ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            },
        })
        { StatusCode = StatusCodes.Status409Conflict, ContentTypes = { "application/problem+json" } };
}
