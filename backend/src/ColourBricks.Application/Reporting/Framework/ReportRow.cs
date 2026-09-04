namespace ColourBricks.Application.Reporting.Framework;

/// <summary>
/// The uniform filter surface every report row exposes. The executor only ever
/// touches these members, so one generic filter path serves every report and
/// adding a report needs no new filter code (P8-T01 acceptance).
/// </summary>
/// <remarks>
/// Implementations must set these as real properties in their LINQ projection —
/// EF Core cannot translate a computed CLR property on a projected type.
/// </remarks>
public interface IReportRow
{
    DateOnly? RowDate { get; }
    long? RowProjectId { get; }
    long? RowPartyId { get; }
    long? RowDepartmentId { get; }
    long? RowItemId { get; }
    long? RowCategoryId { get; }
    long? RowPaymentModeId { get; }
    long? RowAccountId { get; }
    string? RowPaymentStatus { get; }
    string? RowTransactionType { get; }
    string? RowReconciliationStatus { get; }
    string? RowSearchText { get; }
}
