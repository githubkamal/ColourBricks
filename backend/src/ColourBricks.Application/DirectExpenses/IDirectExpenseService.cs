namespace ColourBricks.Application.DirectExpenses;

public interface IDirectExpenseService
{
    Task<DirectExpenseDto> RecordAsync(RecordDirectExpenseRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<DirectExpenseDto>> ListAsync(long projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Reverses a direct expense (client request, 2026-09-04 — a project-attributable
    /// bank charge/fee mapped from a bank statement). Mirrors
    /// <c>CommonExpenseService.ReverseAsync</c>. Returns false if the id does not exist.
    /// </summary>
    Task<bool> ReverseAsync(long id, string reason, CancellationToken cancellationToken);
}
