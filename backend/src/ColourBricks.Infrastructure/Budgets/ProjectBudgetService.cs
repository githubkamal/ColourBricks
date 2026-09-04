using ColourBricks.Application.Budgets;
using ColourBricks.Domain.Budgets;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Budgets;

/// <summary>P5-T01 — versioned project budgets by expense category (BRD §40, §41).</summary>
public sealed class ProjectBudgetService(AppDbContext db) : IProjectBudgetService
{
    public async Task<ProjectBudgetDto?> GetCurrentAsync(long projectId, CancellationToken ct)
    {
        ProjectBudgetRevision? revision = await db.Set<ProjectBudgetRevision>().AsNoTracking()
            .Include(r => r.Lines)
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.RevisionNumber)
            .FirstOrDefaultAsync(ct);

        return revision is null ? null : await ToDtoAsync(projectId, revision, ct);
    }

    public async Task<IReadOnlyList<BudgetRevisionSummaryDto>> ListRevisionsAsync(long projectId, CancellationToken ct)
    {
        List<ProjectBudgetRevision> revisions = await db.Set<ProjectBudgetRevision>().AsNoTracking()
            .Include(r => r.Lines)
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.RevisionNumber)
            .ToListAsync(ct);

        return revisions
            .Select(r => new BudgetRevisionSummaryDto(
                r.RevisionNumber, r.Lines.Sum(l => l.Amount), r.Note, r.CreatedAtUtc, r.CreatedByUserId))
            .ToList();
    }

    public async Task<ProjectBudgetDto?> GetRevisionAsync(long projectId, int revisionNumber, CancellationToken ct)
    {
        ProjectBudgetRevision? revision = await db.Set<ProjectBudgetRevision>().AsNoTracking()
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.RevisionNumber == revisionNumber, ct);

        return revision is null ? null : await ToDtoAsync(projectId, revision, ct);
    }

    public async Task<ProjectBudgetDto> SaveRevisionAsync(
        long projectId, SaveProjectBudgetRequest request, CancellationToken ct)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct))
        {
            throw Fail("projectId", "The project does not exist.");
        }

        if (request.Lines.Count == 0)
        {
            throw Fail("lines", "Give the budget at least one category line.");
        }

        if (request.ApproachingThresholdPercent is < 1m or > 100m)
        {
            throw Fail("approachingThresholdPercent", "The threshold must be between 1 and 100 percent.");
        }

        List<long> categoryIds = request.Lines.Select(l => l.CategoryId).ToList();
        if (categoryIds.Distinct().Count() != categoryIds.Count)
        {
            throw Fail("lines", "A category appears more than once.");
        }

        int known = await db.ExpenseCategories.CountAsync(c => categoryIds.Contains(c.Id), ct);
        if (known != categoryIds.Count)
        {
            throw Fail("lines", "One of the categories does not exist.");
        }

        if (request.Lines.Any(l => l.Amount < 0m))
        {
            throw Fail("lines", "A budget amount cannot be negative.");
        }

        int? maxNumber = await db.Set<ProjectBudgetRevision>()
            .Where(r => r.ProjectId == projectId)
            .Select(r => (int?)r.RevisionNumber)
            .MaxAsync(ct);
        int nextNumber = (maxNumber ?? 0) + 1;

        var revision = new ProjectBudgetRevision
        {
            ProjectId = projectId,
            RevisionNumber = nextNumber,
            ApproachingThresholdPercent = request.ApproachingThresholdPercent,
            Note = request.Note?.Trim(),
            Lines = request.Lines
                .Select(l => new ProjectBudgetLine { CategoryId = l.CategoryId, Amount = Money.Round(l.Amount) })
                .ToList(),
        };
        db.Add(revision);
        await db.SaveChangesAsync(ct);

        return await ToDtoAsync(projectId, revision, ct);
    }

    private async Task<ProjectBudgetDto> ToDtoAsync(long projectId, ProjectBudgetRevision revision, CancellationToken ct)
    {
        decimal estimatedCost = await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId).Select(p => p.EstimatedCost).FirstAsync(ct);

        List<long> categoryIds = revision.Lines.Select(l => l.CategoryId).Distinct().ToList();
        Dictionary<long, (string Name, string Bucket)> categories = await db.ExpenseCategories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => (c.Name, c.Bucket), ct);

        List<BudgetLineDto> lines = revision.Lines
            .Select(l => new BudgetLineDto(
                l.CategoryId,
                categories.GetValueOrDefault(l.CategoryId).Name ?? "",
                categories.GetValueOrDefault(l.CategoryId).Bucket ?? "",
                l.Amount))
            .OrderBy(l => l.CategoryName)
            .ToList();

        decimal total = lines.Sum(l => l.Amount);

        return new ProjectBudgetDto(
            projectId, revision.RevisionNumber, revision.ApproachingThresholdPercent, revision.Note,
            revision.CreatedAtUtc, revision.CreatedByUserId, estimatedCost, total, total - estimatedCost, lines);
    }

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
