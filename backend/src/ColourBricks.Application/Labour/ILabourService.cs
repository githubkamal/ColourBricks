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

    Task<IReadOnlyList<TeamStatementRowDto>> TeamStatementAsync(
        long teamId, CancellationToken cancellationToken);
}
