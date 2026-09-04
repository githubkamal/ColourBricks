using System.Diagnostics;
using ColourBricks.Api.Authorization;
using ColourBricks.Application.Parties;
using ColourBricks.Application.Teams;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Teams;

[ApiController]
[Route("api/v1/teams")]
public sealed class TeamsController(ITeamService teams) : ControllerBase
{
    [HttpGet]
    [HasPermission("labour.view")]
    public Task<IReadOnlyList<TeamDto>> List(
        [FromQuery] long? departmentId,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        teams.ListAsync(departmentId, includeInactive, cancellationToken);

    [HttpGet("grouped")]
    [HasPermission("labour.view")]
    public Task<IReadOnlyList<TeamGroup>> Grouped(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        teams.ListGroupedAsync(includeInactive, cancellationToken);

    [HttpGet("{id:long}")]
    [HasPermission("labour.view")]
    public async Task<ActionResult<TeamDto>> Get(long id, CancellationToken cancellationToken)
    {
        TeamDto? team = await teams.GetAsync(id, cancellationToken);
        return team is null ? NotFound() : Ok(team);
    }

    [HttpPost]
    [HasPermission("labour.add")]
    public async Task<IActionResult> Create(
        [FromBody] CreateTeamRequest request,
        [FromQuery] bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            CreateTeamResult result = await teams.CreateAsync(request, confirm, cancellationToken);

            if (result.RequiresConfirmation)
            {
                return Ok(new { requiresConfirmation = true, nearDuplicates = result.NearDuplicates });
            }

            return CreatedAtAction(nameof(Get), new { id = result.Team!.Id }, result.Team);
        }
        catch (PartyExactDuplicateException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    [HttpPut("{id:long}")]
    [HasPermission("labour.edit")]
    public async Task<ActionResult<TeamDto>> Update(
        long id, [FromBody] UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        try
        {
            TeamDto? updated = await teams.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (PartyExactDuplicateException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    private ObjectResult Conflict409(string detail, long existingId)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "A party with this name already exists.",
            Detail = detail,
            Type = "https://datatracker.ietf.org/doc/html/rfc9457",
            Extensions =
            {
                ["existingId"] = existingId,
                ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            },
        };
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status409Conflict,
            ContentTypes = { "application/problem+json" },
        };
    }
}
