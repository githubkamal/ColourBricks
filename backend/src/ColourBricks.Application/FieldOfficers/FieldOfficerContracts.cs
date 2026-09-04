namespace ColourBricks.Application.FieldOfficers;

/// <summary>
/// A field officer's bill with no project — Personal/Office/Savings/Custom (client
/// request, 2026-09-04). Posts a payable to him rather than paying instantly.
/// </summary>
public sealed record FieldOfficerExpenseDto(
    long Id,
    long FieldOfficerId,
    string Type,
    DateOnly Date,
    decimal Amount,
    string? ReferenceNo,
    string? Description,
    string Status);

public sealed record RecordFieldOfficerExpenseRequest(
    long FieldOfficerId,
    string Type,
    DateOnly Date,
    decimal Amount,
    string? ReferenceNo = null,
    string? Description = null);

public sealed record FieldOfficerExpenseQuery(
    long? FieldOfficerId = null,
    string? Type = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null);

public interface IFieldOfficerExpenseService
{
    /// <summary>
    /// Records a no-project bill: <c>Debit {Personal|Office|Savings|Custom}(no project)</c>
    /// / <c>Credit vendor_payable(no project, party=FieldOfficerId)</c> — the same payable
    /// category and party-generic outstanding/advance derivation a vendor purchase uses.
    /// </summary>
    Task<FieldOfficerExpenseDto> RecordAsync(
        RecordFieldOfficerExpenseRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<FieldOfficerExpenseDto>> ListAsync(
        FieldOfficerExpenseQuery query, CancellationToken cancellationToken);

    /// <summary>Reverses a field officer bill. Mirrors <c>CommonExpenseService.ReverseAsync</c>.</summary>
    Task<bool> ReverseAsync(long id, string reason, CancellationToken cancellationToken);
}
