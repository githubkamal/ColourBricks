namespace ColourBricks.Application.CommonExpenses;

/// <summary>Every expense in the period + types is already allocated (BRD §47, rule 42).</summary>
public sealed class AlreadyAllocatedException(DateOnly from, DateOnly to)
    : Exception($"Every common expense between {from:yyyy-MM-dd} and {to:yyyy-MM-dd} is already allocated.");

public sealed record AllocationProjectShareInput(long ProjectId, decimal Value);

public sealed record CommonExpenseAllocationRequest(
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    IReadOnlyList<string> Types,
    string Method,                                       // Equal | Percentage | Manual
    IReadOnlyList<AllocationProjectShareInput>? Shares = null, // percentages or manual amounts
    string? Note = null);

public sealed record AllocationPreviewLineDto(
    long ProjectId,
    string ProjectName,
    decimal Before,
    decimal Allocated,
    decimal After,
    decimal? Percent);

public sealed record CommonExpenseAllocationPreviewDto(
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    string Method,
    decimal PoolAmount,
    decimal TotalAllocated,
    bool Balances,
    IReadOnlyList<AllocationPreviewLineDto> Lines);

public sealed record CommonExpenseAllocationRunDto(
    long Id,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    string Types,
    string Method,
    decimal PoolAmount,
    string Status,
    string? Note,
    DateTimeOffset CreatedAtUtc,
    long? CreatedByUserId,
    IReadOnlyList<AllocationPreviewLineDto> Lines);

public sealed record CommonExpenseAllocationReportRowDto(
    long RunId,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    string Types,
    string Method,
    decimal PoolAmount,
    long ProjectId,
    string ProjectName,
    decimal Allocated,
    string Status);

public interface ICommonExpenseAllocationService
{
    Task<CommonExpenseAllocationPreviewDto> PreviewAsync(
        CommonExpenseAllocationRequest request, CancellationToken cancellationToken);

    Task<CommonExpenseAllocationRunDto> CommitAsync(
        CommonExpenseAllocationRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<CommonExpenseAllocationRunDto>> ListRunsAsync(CancellationToken cancellationToken);

    Task<CommonExpenseAllocationRunDto?> GetRunAsync(long runId, CancellationToken cancellationToken);

    Task ReverseRunAsync(long runId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CommonExpenseAllocationReportRowDto>> ReportAsync(
        DateOnly? from, DateOnly? to, string? type, CancellationToken cancellationToken);
}
