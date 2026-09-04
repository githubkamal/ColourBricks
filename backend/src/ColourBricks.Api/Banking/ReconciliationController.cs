using ColourBricks.Api.Authorization;
using ColourBricks.Application.Banking;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Banking;

[ApiController]
[Route("api/v1")]
public sealed class ReconciliationController(
    IReconciliationService reconciliation, IMatchSuggestionService matcher) : ControllerBase
{
    [HttpGet("reconciliation")]
    [HasPermission("bank_reconciliation.view")]
    public Task<ReconciliationQueueDto> Queue(
        [FromQuery] long? accountId, [FromQuery] string? status,
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo,
        [FromQuery] decimal? amountMin, [FromQuery] decimal? amountMax,
        [FromQuery] bool? matched, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default) =>
        reconciliation.QueueAsync(
            new ReconciliationQueueQuery(accountId, status, dateFrom, dateTo, amountMin, amountMax,
                matched, search, page, pageSize), cancellationToken);

    [HttpGet("bank-transactions/{id:long}/match-suggestions")]
    [HasPermission("bank_reconciliation.view")]
    public Task<MatchSuggestionsDto> MatchSuggestions(long id, CancellationToken cancellationToken) =>
        matcher.SuggestAsync(id, cancellationToken);

    [HttpGet("bank-transactions/{id:long}/reconciliation-proposal")]
    [HasPermission("bank_reconciliation.view")]
    public Task<ReconciliationProposalDto> Proposal(
        long id, [FromQuery] long? vendorId, CancellationToken cancellationToken) =>
        reconciliation.ProposalAsync(id, vendorId, cancellationToken);

    [HttpPost("bank-transactions/{id:long}/reconcile-credit")]
    [HasPermission("bank_reconciliation.reconcile")]
    public Task<ReconcileResultDto> ReconcileCredit(
        long id, [FromBody] ReconcileCreditRequest request, CancellationToken cancellationToken) =>
        reconciliation.ReconcileCreditAsync(id, request, cancellationToken);

    [HttpPost("bank-transactions/{id:long}/reconcile-debit")]
    [HasPermission("bank_reconciliation.reconcile")]
    public Task<ReconcileResultDto> ReconcileDebit(
        long id, [FromBody] ReconcileDebitRequest request, CancellationToken cancellationToken) =>
        reconciliation.ReconcileDebitAsync(id, request, cancellationToken);

    [HttpPost("bank-transactions/{id:long}/map-debit")]
    [HasPermission("bank_reconciliation.reconcile")]
    public Task<MapDebitResultDto> MapDebit(
        long id, [FromBody] MapDebitRequest request, CancellationToken cancellationToken) =>
        reconciliation.MapDebitAsync(id, request, cancellationToken);

    [HttpPost("bank-transactions/{id:long}/hold")]
    [HasPermission("bank_reconciliation.edit")]
    public async Task<IActionResult> Hold(long id, CancellationToken cancellationToken)
    {
        await reconciliation.HoldAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("bank-transactions/{id:long}/unhold")]
    [HasPermission("bank_reconciliation.edit")]
    public async Task<IActionResult> Unhold(long id, CancellationToken cancellationToken)
    {
        await reconciliation.UnholdAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("bank-transactions/{id:long}/unreconcile")]
    [HasPermission("bank_reconciliation.reconcile")]
    public async Task<IActionResult> Unreconcile(
        long id, [FromBody] UnreconcileRequest request, CancellationToken cancellationToken)
    {
        await reconciliation.UnreconcileAsync(id, request.Reason, cancellationToken);
        return NoContent();
    }

    [HttpPost("bank-transactions/bulk-exclude")]
    [HasPermission("bank_reconciliation.edit")]
    public async Task<IActionResult> BulkExclude(
        [FromBody] BulkExcludeRequest request, CancellationToken cancellationToken)
    {
        await reconciliation.BulkExcludeAsync(request.Ids, request.Reason, cancellationToken);
        return NoContent();
    }

    [HttpGet("internal-transfers/suggestions")]
    [HasPermission("bank_reconciliation.view")]
    public Task<IReadOnlyList<InternalTransferSuggestionDto>> TransferSuggestions(
        [FromQuery] long? accountId, CancellationToken cancellationToken) =>
        reconciliation.TransferSuggestionsAsync(accountId, cancellationToken);

    [HttpPost("internal-transfers")]
    [HasPermission("bank_reconciliation.reconcile")]
    public Task<InternalTransferDto> PairTransfer(
        [FromBody] PairInternalTransferRequest request, CancellationToken cancellationToken) =>
        reconciliation.PairTransferAsync(request, cancellationToken);

    [HttpPost("internal-transfers/{id:long}/unpair")]
    [HasPermission("bank_reconciliation.reconcile")]
    public async Task<IActionResult> UnpairTransfer(long id, CancellationToken cancellationToken)
    {
        await reconciliation.UnpairTransferAsync(id, cancellationToken);
        return NoContent();
    }
}
