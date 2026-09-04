namespace ColourBricks.Application.Budgets;

public sealed record BudgetLineDto(long CategoryId, string CategoryName, string Bucket, decimal Amount);

public sealed record ProjectBudgetDto(
    long ProjectId,
    int RevisionNumber,
    decimal ApproachingThresholdPercent,
    string? Note,
    DateTimeOffset RevisedAtUtc,
    long? RevisedByUserId,
    decimal EstimatedCost,
    decimal BudgetTotal,
    decimal VarianceFromEstimate,
    IReadOnlyList<BudgetLineDto> Lines);

public sealed record BudgetRevisionSummaryDto(
    int RevisionNumber,
    decimal BudgetTotal,
    string? Note,
    DateTimeOffset RevisedAtUtc,
    long? RevisedByUserId);

public sealed record BudgetLineInput(long CategoryId, decimal Amount);

public sealed record SaveProjectBudgetRequest(
    IReadOnlyList<BudgetLineInput> Lines,
    decimal ApproachingThresholdPercent = 90m,
    string? Note = null);

public interface IProjectBudgetService
{
    Task<ProjectBudgetDto?> GetCurrentAsync(long projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<BudgetRevisionSummaryDto>> ListRevisionsAsync(
        long projectId, CancellationToken cancellationToken);

    Task<ProjectBudgetDto?> GetRevisionAsync(long projectId, int revisionNumber, CancellationToken cancellationToken);

    Task<ProjectBudgetDto> SaveRevisionAsync(
        long projectId, SaveProjectBudgetRequest request, CancellationToken cancellationToken);
}
