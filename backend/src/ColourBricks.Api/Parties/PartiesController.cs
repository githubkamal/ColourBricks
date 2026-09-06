using System.Diagnostics;
using ColourBricks.Api.Authorization;
using ColourBricks.Application.Common.Pagination;
using ColourBricks.Application.Parties;
using ColourBricks.Domain.Parties;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Parties;

[ApiController]
[Route("api/v1/parties")]
public sealed class PartiesController(IPartyService parties) : ControllerBase
{
    [HttpGet]
    [HasPermission("vendors.view")]
    public Task<PagedResult<PartySearchItem>> List(
        [FromQuery] PartyType? type,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        parties.ListAsync(type, search, page, pageSize, cancellationToken);

    [HttpGet("search")]
    [HasPermission("vendors.view")]
    public Task<IReadOnlyList<PartySearchItem>> Search(
        [FromQuery] string q,
        [FromQuery] PartyType? type,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default) =>
        parties.SearchAsync(q, type, limit, cancellationToken);

    [HttpGet("{id:long}")]
    [HasPermission("vendors.view")]
    public async Task<ActionResult<PartyDto>> Get(long id, CancellationToken cancellationToken)
    {
        PartyDto? party = await parties.GetAsync(id, cancellationToken);
        return party is null ? NotFound() : Ok(party);
    }

    [HttpPost]
    [HasPermission("vendors.add")]
    public async Task<IActionResult> Create(
        [FromBody] CreatePartyRequest request,
        [FromQuery] bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            CreatePartyResult result = await parties.CreateAsync(request, confirm, cancellationToken);

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

    [HttpPut("{id:long}")]
    [HasPermission("vendors.edit")]
    public async Task<ActionResult<PartyDto>> Update(
        long id, [FromBody] UpdatePartyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            PartyDto? party = await parties.UpdateAsync(id, request, cancellationToken);
            return party is null ? NotFound() : Ok(party);
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
