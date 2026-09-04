using System.Diagnostics;
using ColourBricks.Api.Authorization;
using ColourBricks.Application.Parties;
using ColourBricks.Application.Temples;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Temples;

[ApiController]
[Route("api/v1/temples")]
public sealed class TemplesController(ITempleService temples) : ControllerBase
{
    [HttpGet]
    [HasPermission("temple_donations.view")]
    public Task<IReadOnlyList<PartySearchItem>> List(
        [FromQuery] string? search, CancellationToken cancellationToken = default) =>
        temples.ListAsync(search, cancellationToken);

    [HttpGet("search")]
    [HasPermission("temple_donations.view")]
    public Task<IReadOnlyList<PartySearchItem>> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default) =>
        temples.SearchAsync(q, limit, cancellationToken);

    [HttpGet("{id:long}")]
    [HasPermission("temple_donations.view")]
    public async Task<ActionResult<PartyDto>> Get(long id, CancellationToken cancellationToken)
    {
        PartyDto? temple = await temples.GetAsync(id, cancellationToken);
        return temple is null ? NotFound() : Ok(temple);
    }

    [HttpPost]
    [HasPermission("temple_donations.add")]
    public async Task<IActionResult> Create(
        [FromBody] CreateTempleRequest request,
        [FromQuery] bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            CreatePartyResult result = await temples.CreateAsync(request.Name, confirm, cancellationToken);

            if (result.RequiresConfirmation)
            {
                return Ok(new { requiresConfirmation = true, nearDuplicates = result.NearDuplicates });
            }

            return CreatedAtAction(nameof(Get), new { id = result.Party!.Id }, result.Party);
        }
        catch (PartyExactDuplicateException ex)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "A party with this name already exists.",
                Detail = ex.Message,
                Type = "https://datatracker.ietf.org/doc/html/rfc9457",
                Extensions =
                {
                    ["existingId"] = ex.ExistingId,
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
}

public sealed record CreateTempleRequest(string Name);
