using System.Diagnostics;
using ColourBricks.Api.Authorization;
using ColourBricks.Application.Accounts;
using ColourBricks.Domain.Accounts;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Accounts;

[ApiController]
[Route("api/v1/accounts")]
public sealed class AccountsController(IAccountService accounts) : ControllerBase
{
    // BRD §29 — account numbers are shown in full only to an Administrator
    // (a full-access token collapses the permission list to "*").
    private bool Unmasked => User.FindFirst("permissions")?.Value == "*";

    [HttpGet]
    [HasPermission("accounts.view")]
    public Task<IReadOnlyList<AccountListItemDto>> List(
        [FromQuery] AccountType? type,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        accounts.ListAsync(type, includeInactive, Unmasked, cancellationToken);

    [HttpGet("{id:long}")]
    [HasPermission("accounts.view")]
    public async Task<ActionResult<AccountDto>> Get(long id, CancellationToken cancellationToken)
    {
        AccountDto? account = await accounts.GetAsync(id, Unmasked, cancellationToken);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPost]
    [HasPermission("accounts.add")]
    public async Task<IActionResult> Create(
        [FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
    {
        try
        {
            AccountDto account = await accounts.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = account.Id }, account);
        }
        catch (AccountExactDuplicateException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    [HttpPut("{id:long}")]
    [HasPermission("accounts.edit")]
    public async Task<ActionResult<AccountDto>> Update(
        long id, [FromBody] UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        try
        {
            AccountDto? updated = await accounts.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (AccountExactDuplicateException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    private ObjectResult Conflict409(string detail, long existingId)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "An account with this name already exists.",
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
