using ColourBricks.Api.Authorization;
using ColourBricks.Application.Labour;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Labour;

[ApiController]
[Route("api/v1/labour")]
public sealed class LabourController(ILabourService labour) : ControllerBase
{
    [HttpPost("work")]
    [HasPermission("labour.add")]
    public async Task<ActionResult<WorkEntryDto>> RecordWork(
        [FromBody] RecordWorkRequest request, CancellationToken cancellationToken)
    {
        WorkEntryDto work = await labour.RecordWorkAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetWork), new { id = work.Id }, work);
    }

    [HttpGet("work/{id:long}")]
    [HasPermission("labour.view")]
    public async Task<ActionResult<WorkEntryDto>> GetWork(long id, CancellationToken cancellationToken)
    {
        WorkEntryDto? work = await labour.GetWorkAsync(id, cancellationToken);
        return work is null ? NotFound() : Ok(work);
    }

    [HttpGet("work")]
    [HasPermission("labour.view")]
    public Task<IReadOnlyList<WorkEntryDto>> ListWork(
        [FromQuery] long? projectId,
        [FromQuery] long? teamId,
        CancellationToken cancellationToken = default) =>
        labour.ListWorkAsync(projectId, teamId, cancellationToken);

    [HttpPost("work/{id:long}/payments")]
    [HasPermission("labour.edit")]
    public async Task<ActionResult<WorkPaymentDto>> Pay(
        long id, [FromBody] PayWorkRequest request, CancellationToken cancellationToken)
    {
        WorkPaymentDto payment = await labour.PayAsync(id, request, cancellationToken);
        return Ok(payment);
    }

    [HttpGet("teams/{teamId:long}/statement")]
    [HasPermission("labour.view")]
    public Task<IReadOnlyList<TeamStatementRowDto>> Statement(
        long teamId, CancellationToken cancellationToken) =>
        labour.TeamStatementAsync(teamId, cancellationToken);
}
