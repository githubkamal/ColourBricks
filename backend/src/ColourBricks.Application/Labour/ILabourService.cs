namespace ColourBricks.Application.Labour;

public interface ILabourService
{
    Task<WorkEntryDto> RecordWorkAsync(RecordWorkRequest request, CancellationToken cancellationToken);

    Task<WorkEntryDto?> GetWorkAsync(long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkEntryDto>> ListWorkAsync(
        long? projectId, long? teamId, CancellationToken cancellationToken);

    /// <summary>
    /// Records a partial/scheduled payment against a work entry. Rejects an amount
    /// that would take total paid past the agreed value (advances are out of scope).
    /// </summary>
    Task<WorkPaymentDto> PayAsync(long workEntryId, PayWorkRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Reverses a work entry so it can be corrected (client request, 2026-09-19 — edit
    /// support for labour/subcontractor work). Mirrors <c>DirectExpenseService.ReverseAsync</c>.
    /// Rejects an entry that already has a payment recorded — reversing it would leave that
    /// payment's settlement pointing at a reversed obligation. Returns false if the id does
    /// not exist.
    /// </summary>
    Task<bool> ReverseAsync(long id, string reason, CancellationToken cancellationToken);

    Task<IReadOnlyList<TeamStatementRowDto>> TeamStatementAsync(
        long teamId, CancellationToken cancellationToken);
}
