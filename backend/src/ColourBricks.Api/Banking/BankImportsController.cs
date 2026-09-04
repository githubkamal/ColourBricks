using ColourBricks.Api.Authorization;
using ColourBricks.Application.Banking;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Banking;

[ApiController]
[Route("api/v1")]
public sealed class BankImportsController(
    IBankImportService imports, IBankStatementParser parser) : ControllerBase
{
    private const long MaxStatementBytes = 25 * 1024 * 1024;

    /// <summary>Stage a parsed statement for review. Nothing reaches <c>BankTransaction</c> until commit.</summary>
    [HttpPost("bank-imports")]
    [HasPermission("bank_reconciliation.add")]
    public async Task<ActionResult<BankImportBatchDto>> Create(
        [FromBody] CreateBankImportRequest request, CancellationToken cancellationToken)
    {
        BankImportBatchDto batch = await imports.CreateDraftAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { batchId = batch.Id }, batch);
    }

    /// <summary>Return a statement's header cells + sample rows so the mapping wizard can bind columns.</summary>
    [HttpPost("bank-imports/detect-columns")]
    [HasPermission("bank_reconciliation.add")]
    [RequestSizeLimit(MaxStatementBytes)]
    public async Task<ActionResult<DetectedColumnsDto>> DetectColumns(
        IFormFile file, [FromForm] int headerRowIndex = 0)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "A non-empty file is required.");
        }

        await using MemoryStream buffer = new();
        await file.CopyToAsync(buffer);
        buffer.Position = 0;
        return Ok(parser.DetectColumns(buffer, file.FileName, Math.Max(0, headerRowIndex)));
    }

    /// <summary>Parse an uploaded statement against a saved profile and stage it as a Draft batch.</summary>
    [HttpPost("bank-imports/upload")]
    [HasPermission("bank_reconciliation.add")]
    [RequestSizeLimit(MaxStatementBytes)]
    public async Task<ActionResult<BankImportBatchDto>> Upload(
        [FromForm] long accountId, [FromForm] long profileId, IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "A non-empty file is required.");
        }

        await using MemoryStream buffer = new();
        await file.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        BankImportBatchDto batch = await imports.CreateDraftFromFileAsync(
            accountId, file.FileName, buffer, profileId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { batchId = batch.Id }, batch);
    }

    [HttpGet("bank-imports/{batchId:long}")]
    [HasPermission("bank_reconciliation.view")]
    public async Task<ActionResult<BankImportBatchDto>> Get(long batchId, CancellationToken cancellationToken)
    {
        BankImportBatchDto? batch = await imports.GetAsync(batchId, cancellationToken);
        return batch is null ? NotFound() : Ok(batch);
    }

    [HttpPut("bank-imports/{batchId:long}/rows/{rowId:long}/allocations")]
    [HasPermission("bank_reconciliation.add")]
    public Task<StagedBankRowDto> SetRowAllocations(
        long batchId, long rowId,
        [FromBody] SetRowAllocationsRequest request, CancellationToken cancellationToken) =>
        imports.SetRowAllocationsAsync(batchId, rowId, request, cancellationToken);

    [HttpDelete("bank-imports/{batchId:long}/rows/{rowId:long}")]
    [HasPermission("bank_reconciliation.add")]
    public async Task<IActionResult> RemoveRow(
        long batchId, long rowId, [FromQuery] string reason, CancellationToken cancellationToken)
    {
        await imports.RemoveRowAsync(batchId, rowId, reason, cancellationToken);
        return NoContent();
    }

    [HttpPost("bank-imports/{batchId:long}/commit")]
    [HasPermission("bank_reconciliation.add")]
    public Task<BankImportCommitResultDto> Commit(long batchId, CancellationToken cancellationToken) =>
        imports.CommitAsync(batchId, cancellationToken);

    [HttpPost("bank-imports/{batchId:long}/discard")]
    [HasPermission("bank_reconciliation.add")]
    public async Task<IActionResult> Discard(long batchId, CancellationToken cancellationToken)
    {
        bool done = await imports.DiscardAsync(batchId, cancellationToken);
        return done ? NoContent() : NotFound();
    }

    [HttpGet("bank-transactions/{id:long}")]
    [HasPermission("bank_reconciliation.view")]
    public async Task<ActionResult<BankTransactionDto>> GetTransaction(long id, CancellationToken cancellationToken)
    {
        BankTransactionDto? tx = await imports.GetTransactionAsync(id, cancellationToken);
        return tx is null ? NotFound() : Ok(tx);
    }

    [HttpPost("bank-transactions/{id:long}/exclude")]
    [HasPermission("bank_reconciliation.edit")]
    public async Task<IActionResult> Exclude(
        long id, [FromBody] ExcludeBankTransactionRequest request, CancellationToken cancellationToken)
    {
        bool done = await imports.ExcludeTransactionAsync(id, request.Reason, cancellationToken);
        return done ? NoContent() : NotFound();
    }
}
