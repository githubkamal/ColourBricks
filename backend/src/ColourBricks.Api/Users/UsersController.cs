using System.Diagnostics;
using ColourBricks.Api.Authorization;
using ColourBricks.Application.Users;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Users;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(IUserAdminService users) : ControllerBase
{
    [HttpGet]
    [HasPermission("users.view")]
    public Task<IReadOnlyList<UserListItemDto>> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        users.ListAsync(includeInactive, cancellationToken);

    [HttpGet("roles")]
    [HasPermission("users.view")]
    public Task<IReadOnlyList<RoleOptionDto>> Roles(CancellationToken cancellationToken) =>
        users.ListRolesAsync(cancellationToken);

    [HttpGet("{id:long}")]
    [HasPermission("users.view")]
    public async Task<ActionResult<UserListItemDto>> Get(long id, CancellationToken cancellationToken)
    {
        UserListItemDto? user = await users.GetAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [HasPermission("users.add")]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            UserListItemDto user = await users.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
        }
        catch (UserEmailInUseException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    [HttpPut("{id:long}")]
    [HasPermission("users.edit")]
    public async Task<ActionResult<UserListItemDto>> Update(
        long id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            UserListItemDto? updated = await users.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (UserEmailInUseException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
        catch (LastAdministratorException ex)
        {
            return Problem400(ex.Message);
        }
    }

    [HttpDelete("{id:long}")]
    [HasPermission("users.delete")]
    public async Task<IActionResult> Delete(
        long id, [FromQuery] string reason, CancellationToken cancellationToken)
    {
        try
        {
            return await users.DeleteAsync(id, reason, cancellationToken) ? NoContent() : NotFound();
        }
        catch (LastAdministratorException ex)
        {
            return Problem400(ex.Message);
        }
        catch (UserHasHistoryException ex)
        {
            return Problem400(ex.Message);
        }
    }

    [HttpPut("{id:long}/projects")]
    [HasPermission("users.edit")]
    public async Task<ActionResult<UserListItemDto>> AssignProjects(
        long id, [FromBody] AssignProjectsRequest request, CancellationToken cancellationToken)
    {
        UserListItemDto? updated = await users.AssignProjectsAsync(id, request.ProjectIds, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("{id:long}/reset-password")]
    [HasPermission("users.edit")]
    public async Task<IActionResult> ResetPassword(
        long id, [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        bool done = await users.ResetPasswordAsync(id, request.NewPassword, cancellationToken);
        return done ? NoContent() : NotFound();
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
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" },
        };

    private ObjectResult Conflict409(string detail, long existingId) =>
        new(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "A user with this email already exists.",
            Detail = detail,
            Type = "https://datatracker.ietf.org/doc/html/rfc9457",
            Extensions =
            {
                ["existingId"] = existingId,
                ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            },
        })
        {
            StatusCode = StatusCodes.Status409Conflict,
            ContentTypes = { "application/problem+json" },
        };
}
