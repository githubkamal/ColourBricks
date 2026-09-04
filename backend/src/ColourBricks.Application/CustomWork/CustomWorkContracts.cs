namespace ColourBricks.Application.CustomWork;

public sealed record CustomWorkDto(
    long Id,
    long ProjectId,
    long? PartyId,
    string? PartyName,
    long? DepartmentId,
    DateOnly Date,
    string? WorkType,
    string? Description,
    decimal EstimatedCost,
    decimal ActualCost,
    decimal Variance,
    string Status);

public sealed record RecordCustomWorkRequest(
    long ProjectId,
    DateOnly Date,
    decimal EstimatedCost,
    decimal ActualCost,
    long? PartyId = null,
    long? DepartmentId = null,
    string? WorkType = null,
    string? Description = null);

public interface ICustomWorkService
{
    Task<CustomWorkDto> RecordAsync(RecordCustomWorkRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomWorkDto>> ListAsync(long projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Reverses the custom-work record itself (a wrong entry), not a payment against it
    /// (client request, 2026-09-04). Mirrors <c>CommonExpenseService.ReverseAsync</c>.
    /// Returns false if the id does not exist.
    /// </summary>
    Task<bool> ReverseAsync(long id, string reason, CancellationToken cancellationToken);
}
