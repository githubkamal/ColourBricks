using ColourBricks.Domain.Projects;

namespace ColourBricks.Application.Projects;

public sealed record ProjectDto(
    long Id,
    string Code,
    string Name,
    long? ClientId,
    string? SiteAddress,
    string? ContactDetails,
    DateOnly StartDate,
    DateOnly? ExpectedEndDate,
    DateOnly? ActualEndDate,
    decimal ContractValue,
    decimal EstimatedCost,
    decimal? ExpectedProfit,
    ProjectStatus Status,
    long? ManagerId,
    string? Notes,
    bool IsActive,
    string ConcurrencyStamp);

public sealed record ProjectListItemDto(
    long Id,
    string Code,
    string Name,
    ProjectStatus Status,
    DateOnly StartDate,
    DateOnly? ExpectedEndDate,
    decimal ContractValue,
    decimal EstimatedCost,
    long? ManagerId);

public sealed record CreateProjectRequest(
    string Name,
    string? Code,
    long? ClientId,
    string? SiteAddress,
    string? ContactDetails,
    DateOnly StartDate,
    DateOnly? ExpectedEndDate,
    DateOnly? ActualEndDate,
    decimal ContractValue,
    decimal? EstimatedCost,
    decimal? ExpectedProfit,
    ProjectStatus? Status,
    long? ManagerId,
    string? Notes);

public sealed record UpdateProjectRequest(
    string Name,
    long? ClientId,
    string? SiteAddress,
    string? ContactDetails,
    DateOnly StartDate,
    DateOnly? ExpectedEndDate,
    DateOnly? ActualEndDate,
    decimal ContractValue,
    decimal EstimatedCost,
    decimal? ExpectedProfit,
    ProjectStatus Status,
    long? ManagerId,
    string? Notes,
    string ConcurrencyStamp);

public sealed record ProjectListQuery(
    ProjectStatus? Status,
    string? Search,
    int Page,
    int PageSize,
    string? SortBy,
    string? SortDir);
