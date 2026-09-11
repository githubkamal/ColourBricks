using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Auth;
using ColourBricks.Application.Common.Pagination;
using ColourBricks.Application.Donations;
using ColourBricks.Application.Projects;
using ColourBricks.Domain.Projects;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Projects;

public sealed class ProjectService(
    AppDbContext db,
    IProjectDonationService donations,
    IProjectScopeFilter scopeFilter) : IProjectService
{
    private const int MaxCodeAttempts = 10;

    public async Task<PagedResult<ProjectListItemDto>> ListAsync(
        ProjectListQuery query, CancellationToken cancellationToken)
    {
        IQueryable<Project> projects = db.Projects.AsNoTracking().Where(p => p.IsActive);

        // BRD §64 — a project-restricted user only sees their assigned projects.
        ProjectScope scope = await scopeFilter.GetScopeAsync(cancellationToken);
        if (!scope.IsUnrestricted)
        {
            List<long> allowed = scope.ProjectIds.ToList();
            projects = projects.Where(p => allowed.Contains(p.Id));
        }

        if (query.Status is { } status)
        {
            projects = projects.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            projects = projects.Where(p => p.Name.Contains(term) || p.Code.Contains(term));
        }

        projects = ApplySort(projects, query.SortBy, query.SortDir);

        int page = query.Page < 1 ? 1 : query.Page;
        int pageSize = Math.Clamp(query.PageSize < 1 ? 50 : query.PageSize, 1, 200);

        int totalCount = await projects.CountAsync(cancellationToken);
        List<ProjectListItemDto> items = await projects
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToListItem)
            .ToListAsync(cancellationToken);

        return PagedResult<ProjectListItemDto>.Create(items, page, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<ProjectListItemDto>> ListForReportingAsync(CancellationToken cancellationToken)
    {
        // Every project, every status — completed/cancelled included (BRD §70 rule 31).
        IQueryable<Project> projects = db.Projects.AsNoTracking();

        ProjectScope scope = await scopeFilter.GetScopeAsync(cancellationToken);
        if (!scope.IsUnrestricted)
        {
            List<long> allowed = scope.ProjectIds.ToList();
            projects = projects.Where(p => allowed.Contains(p.Id));
        }

        return await projects
            .OrderBy(p => p.Code)
            .Select(ToListItem)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectDto?> GetAsync(long id, CancellationToken cancellationToken)
    {
        ProjectScope scope = await scopeFilter.GetScopeAsync(cancellationToken);
        if (!scope.Allows(id))
        {
            return null;
        }

        Project? project = await db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return project is null ? null : ToDto(project);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        bool manualCode = !string.IsNullOrWhiteSpace(request.Code);

        if (manualCode
            && await db.Projects.AnyAsync(p => p.Code == request.Code, cancellationToken))
        {
            throw Duplicate(request.Code!);
        }

        var project = new Project
        {
            Code = manualCode ? request.Code!.Trim() : await GenerateCodeAsync(request.StartDate.Year, cancellationToken),
            Name = request.Name.Trim(),
            ClientId = request.ClientId,
            SiteAddress = request.SiteAddress,
            ContactDetails = request.ContactDetails,
            StartDate = request.StartDate,
            ExpectedEndDate = request.ExpectedEndDate,
            ActualEndDate = request.ActualEndDate,
            ContractValue = request.ContractValue,
            EstimatedCost = request.EstimatedCost ?? 0m,
            ExpectedProfit = request.ExpectedProfit,
            Status = request.Status ?? ProjectStatus.Ongoing,
            ManagerId = request.ManagerId,
            Notes = request.Notes,
        };

        for (int attempt = 0; ; attempt++)
        {
            db.Projects.Add(project);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return ToDto(project);
            }
            catch (DbUpdateException) when (!manualCode && attempt < MaxCodeAttempts)
            {
                // Lost a race for the sequence — take the next one.
                db.Entry(project).State = EntityState.Detached;
                project.Code = await GenerateCodeAsync(request.StartDate.Year, cancellationToken);
            }
            catch (DbUpdateException) when (manualCode)
            {
                throw Duplicate(project.Code);
            }
        }
    }

    public async Task<ProjectDto?> UpdateAsync(
        long id, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        Project? project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null)
        {
            return null;
        }

        db.Entry(project).Property(p => p.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;

        decimal previousContractValue = project.ContractValue;

        project.Name = request.Name.Trim();
        project.ClientId = request.ClientId;
        project.SiteAddress = request.SiteAddress;
        project.ContactDetails = request.ContactDetails;
        project.StartDate = request.StartDate;
        project.ExpectedEndDate = request.ExpectedEndDate;
        project.ActualEndDate = request.ActualEndDate;
        project.ContractValue = request.ContractValue;
        project.EstimatedCost = request.EstimatedCost;
        project.ExpectedProfit = request.ExpectedProfit;
        project.Status = request.Status;
        project.ManagerId = request.ManagerId;
        project.Notes = request.Notes;

        await db.SaveChangesAsync(cancellationToken);

        // BRD §26 — a percentage-basis temple donation follows the contract value.
        if (request.ContractValue != previousContractValue)
        {
            await donations.RecomputeForContractValueAsync(id, request.ContractValue, cancellationToken);
        }

        return ToDto(project);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        Project? project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null || !project.IsActive)
        {
            return false;
        }

        project.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<string> GenerateCodeAsync(int year, CancellationToken cancellationToken)
    {
        string prefix = $"CB-{year}-";
        int used = await db.Projects.CountAsync(p => p.Code.StartsWith(prefix), cancellationToken);
        return $"{prefix}{used + 1:D3}";
    }

    private static IQueryable<Project> ApplySort(IQueryable<Project> query, string? sortBy, string? sortDir)
    {
        bool descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        return (sortBy?.ToLowerInvariant()) switch
        {
            "name" => descending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "status" => descending ? query.OrderByDescending(p => p.Status) : query.OrderBy(p => p.Status),
            "startdate" => descending ? query.OrderByDescending(p => p.StartDate) : query.OrderBy(p => p.StartDate),
            "contractvalue" => descending
                ? query.OrderByDescending(p => p.ContractValue)
                : query.OrderBy(p => p.ContractValue),
            _ => descending ? query.OrderByDescending(p => p.Code) : query.OrderBy(p => p.Code),
        };
    }

    private static ValidationException Duplicate(string code) =>
        new([new ValidationFailure("code", $"A project with code '{code}' already exists.")]);

    private static readonly System.Linq.Expressions.Expression<Func<Project, ProjectListItemDto>> ToListItem =
        p => new ProjectListItemDto(
            p.Id, p.Code, p.Name, p.Status, p.StartDate, p.ExpectedEndDate,
            p.ContractValue, p.EstimatedCost, p.ManagerId, p.IsActive);

    private static ProjectDto ToDto(Project p) => new(
        p.Id, p.Code, p.Name, p.ClientId, p.SiteAddress, p.ContactDetails,
        p.StartDate, p.ExpectedEndDate, p.ActualEndDate, p.ContractValue, p.EstimatedCost,
        p.ExpectedProfit, p.Status, p.ManagerId, p.Notes, p.IsActive, p.ConcurrencyStamp);
}
