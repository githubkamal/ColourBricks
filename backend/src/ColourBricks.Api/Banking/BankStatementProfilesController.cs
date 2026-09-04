using ColourBricks.Api.Authorization;
using ColourBricks.Application.Banking;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Banking;

[ApiController]
[Route("api/v1")]
public sealed class BankStatementProfilesController(IBankStatementProfileService profiles) : ControllerBase
{
    [HttpPost("bank-statement-profiles")]
    [HasPermission("bank_reconciliation.add")]
    public async Task<ActionResult<BankStatementProfileDto>> Create(
        [FromBody] CreateBankStatementProfileRequest request, CancellationToken cancellationToken)
    {
        BankStatementProfileDto dto = await profiles.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpGet("bank-statement-profiles/{id:long}")]
    [HasPermission("bank_reconciliation.view")]
    public async Task<ActionResult<BankStatementProfileDto>> Get(long id, CancellationToken cancellationToken)
    {
        BankStatementProfileDto? dto = await profiles.GetAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("accounts/{accountId:long}/bank-statement-profiles")]
    [HasPermission("bank_reconciliation.view")]
    public Task<IReadOnlyList<BankStatementProfileDto>> ListForAccount(
        long accountId, CancellationToken cancellationToken) =>
        profiles.ListForAccountAsync(accountId, cancellationToken);
}
