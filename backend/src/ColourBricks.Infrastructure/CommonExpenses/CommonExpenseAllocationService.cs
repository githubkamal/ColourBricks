using ColourBricks.Application.CommonExpenses;
using ColourBricks.Application.Ledger;
using ColourBricks.Domain.CommonExpenses;
using ColourBricks.Domain.Projects;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.CommonExpenses;

/// <summary>
/// P6-T02/T03/T04/T05 — pool a period's unallocated common expenses and distribute
/// them across the ongoing projects (BRD §45–§47). Equal / Percentage / Manual, each
/// summing to the pool exactly via <see cref="AmountSplitter"/>. Preview posts
/// nothing; commit shifts each slice from company level onto the project's ledger.
/// </summary>
public sealed class CommonExpenseAllocationService(
    AppDbContext db,
    ILedgerPostingService ledger,
    IExpenseCategoryService categories) : ICommonExpenseAllocationService
{
    private const string SourceType = "CommonExpenseAllocation";

    private static readonly Dictionary<CommonExpenseType, string> CategorySlug = new()
    {
        [CommonExpenseType.Personal] = "personal_common",
        [CommonExpenseType.Office] = "office_common",
        [CommonExpenseType.Savings] = "savings_allocation",
        [CommonExpenseType.Custom] = "custom_common",
    };

    public async Task<CommonExpenseAllocationPreviewDto> PreviewAsync(
        CommonExpenseAllocationRequest request, CancellationToken ct)
    {
        (List<Project> projects, List<PoolPart> pool, Dictionary<long, decimal> shares, string method) =
            await ResolveAsync(request, ct);

        decimal total = pool.Sum(p => p.Amount);
        Dictionary<long, decimal> beforeByProject = await ProjectCostsAsync(projects.Select(p => p.Id).ToList(), ct);

        var lines = projects
            .Select(p =>
            {
                decimal allocated = Money.Round(shares.GetValueOrDefault(p.Id, 0m));
                decimal before = beforeByProject.GetValueOrDefault(p.Id, 0m);
                return new AllocationPreviewLineDto(
                    p.Id, p.Name, before, allocated, Money.Round(before + allocated),
                    request.Method.Equals(CommonExpenseAllocationMethod.Percentage, StringComparison.OrdinalIgnoreCase)
                        ? PercentFor(request, p.Id)
                        : null);
            })
            .OrderBy(l => l.ProjectName)
            .ToList();

        decimal totalAllocated = Money.Round(lines.Sum(l => l.Allocated));
        return new CommonExpenseAllocationPreviewDto(
            request.PeriodFrom, request.PeriodTo, method, Money.Round(total), totalAllocated,
            Math.Abs(totalAllocated - Money.Round(total)) <= Money.Tolerance, lines);
    }

    public async Task<CommonExpenseAllocationRunDto> CommitAsync(
        CommonExpenseAllocationRequest request, CancellationToken ct)
    {
        (List<Project> projects, List<PoolPart> pool, Dictionary<long, decimal> shares, string method) =
            await ResolveAsync(request, ct);

        decimal total = pool.Sum(p => p.Amount);
        var run = new CommonExpenseAllocationRun
        {
            PeriodFrom = request.PeriodFrom,
            PeriodTo = request.PeriodTo,
            Types = string.Join(",", request.Types.Select(Capitalise)),
            Method = method,
            PoolAmount = Money.Round(total),
            Status = CommonExpenseStatus.Active,
            Note = request.Note?.Trim(),
            Lines = projects.Select(p => new CommonExpenseAllocationLine
            {
                ProjectId = p.Id,
                Amount = Money.Round(shares.GetValueOrDefault(p.Id, 0m)),
                Percent = method == CommonExpenseAllocationMethod.Percentage ? PercentFor(request, p.Id) : null,
            }).ToList(),
        };
        db.Set<CommonExpenseAllocationRun>().Add(run);
        await db.SaveChangesAsync(ct);

        // Move each project's slice from company level onto that project, per source category.
        var legs = new List<LedgerLeg>();
        foreach (Project p in projects)
        {
            decimal projShare = Money.Round(shares.GetValueOrDefault(p.Id, 0m));
            if (projShare <= 0m)
            {
                continue;
            }

            decimal allocatedSoFar = 0m;
            for (int i = 0; i < pool.Count; i++)
            {
                bool last = i == pool.Count - 1;
                decimal slice = last
                    ? projShare - allocatedSoFar
                    : FloorPaisa(projShare * pool[i].Amount / total);
                allocatedSoFar += slice;
                if (slice == 0m)
                {
                    continue;
                }

                legs.Add(new LedgerLeg(pool[i].CategoryId, Debit: slice, Credit: 0m, ProjectId: p.Id));
                legs.Add(new LedgerLeg(pool[i].CategoryId, Debit: 0m, Credit: slice));
            }
        }

        if (legs.Count > 0)
        {
            await ledger.PostAsync(new LedgerPosting(SourceType, run.Id, request.PeriodTo, legs), ct);
        }

        List<CommonExpense> expenses = await UnallocatedAsync(request, ct);
        foreach (CommonExpense e in expenses)
        {
            e.AllocationRunId = run.Id;
        }

        await db.SaveChangesAsync(ct);
        return await GetRunAsync(run.Id, ct) ?? throw new InvalidOperationException();
    }

    public async Task<IReadOnlyList<CommonExpenseAllocationRunDto>> ListRunsAsync(CancellationToken ct)
    {
        List<CommonExpenseAllocationRun> runs = await db.Set<CommonExpenseAllocationRun>().AsNoTracking()
            .Include(r => r.Lines)
            .OrderByDescending(r => r.Id)
            .ToListAsync(ct);

        var result = new List<CommonExpenseAllocationRunDto>();
        foreach (CommonExpenseAllocationRun r in runs)
        {
            result.Add(await ToDtoAsync(r, ct));
        }

        return result;
    }

    public async Task<CommonExpenseAllocationRunDto?> GetRunAsync(long runId, CancellationToken ct)
    {
        CommonExpenseAllocationRun? run = await db.Set<CommonExpenseAllocationRun>().AsNoTracking()
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        return run is null ? null : await ToDtoAsync(run, ct);
    }

    public async Task ReverseRunAsync(long runId, CancellationToken ct)
    {
        CommonExpenseAllocationRun? run = await db.Set<CommonExpenseAllocationRun>()
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw Fail("id", "The allocation run does not exist.");

        if (run.Status == CommonExpenseStatus.Reversed)
        {
            throw Fail("id", "This allocation run is already reversed.");
        }

        await ledger.ReverseAsync(SourceType, runId, "Allocation run reversed", ct);

        List<CommonExpense> expenses = await db.Set<CommonExpense>()
            .Where(e => e.AllocationRunId == runId).ToListAsync(ct);
        foreach (CommonExpense e in expenses)
        {
            e.AllocationRunId = null;
        }

        run.Status = CommonExpenseStatus.Reversed;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CommonExpenseAllocationReportRowDto>> ReportAsync(
        DateOnly? from, DateOnly? to, string? type, CancellationToken ct)
    {
        IQueryable<CommonExpenseAllocationRun> query = db.Set<CommonExpenseAllocationRun>().AsNoTracking()
            .Include(r => r.Lines)
            .Where(r => r.Status == CommonExpenseStatus.Active);
        if (from is { } f) query = query.Where(r => r.PeriodTo >= f);
        if (to is { } t) query = query.Where(r => r.PeriodFrom <= t);

        List<CommonExpenseAllocationRun> runs = await query.OrderByDescending(r => r.PeriodFrom).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(type))
        {
            runs = runs.Where(r => r.Types.Split(',').Contains(Capitalise(type))).ToList();
        }

        List<long> projectIds = runs.SelectMany(r => r.Lines.Select(l => l.ProjectId)).Distinct().ToList();
        Dictionary<long, string> names = await db.Projects.AsNoTracking()
            .Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        return runs
            .SelectMany(r => r.Lines.Select(l => new CommonExpenseAllocationReportRowDto(
                r.Id, r.PeriodFrom, r.PeriodTo, r.Types, r.Method, r.PoolAmount,
                l.ProjectId, names.GetValueOrDefault(l.ProjectId, ""), l.Amount, r.Status.ToString())))
            .ToList();
    }

    // ── shared resolution ────────────────────────────────────────────────────

    private sealed record PoolPart(long CategoryId, decimal Amount);

    private async Task<(List<Project> Projects, List<PoolPart> Pool, Dictionary<long, decimal> Shares, string Method)>
        ResolveAsync(CommonExpenseAllocationRequest request, CancellationToken ct)
    {
        if (!CommonExpenseAllocationMethod.IsValid(Capitalise(request.Method)))
        {
            throw Fail("method", "Method must be Equal, Percentage or Manual.");
        }

        string method = Capitalise(request.Method);
        if (request.PeriodFrom > request.PeriodTo)
        {
            throw Fail("periodTo", "The period end is before its start.");
        }

        List<Project> projects = await db.Projects.AsNoTracking()
            .Where(p => p.Status == ProjectStatus.Ongoing)
            .OrderBy(p => p.Id)
            .ToListAsync(ct);
        if (projects.Count == 0)
        {
            throw Fail("projects", "There are no ongoing projects to allocate to (rule 39).");
        }

        List<CommonExpense> expenses = await UnallocatedAsync(request, ct);
        if (expenses.Count == 0)
        {
            throw new AlreadyAllocatedException(request.PeriodFrom, request.PeriodTo);
        }

        var poolByType = expenses
            .GroupBy(e => e.Type)
            .Select(g => (Type: g.Key, Amount: g.Sum(x => x.Amount)))
            .ToList();

        var pool = new List<PoolPart>();
        foreach ((CommonExpenseType t, decimal amount) in poolByType.OrderBy(x => x.Type))
        {
            pool.Add(new PoolPart(await categories.RequireIdAsync(CategorySlug[t], ct), Money.Round(amount)));
        }

        decimal total = pool.Sum(p => p.Amount);
        Dictionary<long, decimal> shares = method switch
        {
            CommonExpenseAllocationMethod.Equal => Zip(projects, AmountSplitter.SplitEqually(total, projects.Count)),
            CommonExpenseAllocationMethod.Percentage => Percentage(projects, request, total),
            _ => Manual(projects, request, total),
        };

        return (projects, pool, shares, method);
    }

    private async Task<List<CommonExpense>> UnallocatedAsync(CommonExpenseAllocationRequest request, CancellationToken ct)
    {
        var types = request.Types
            .Select(t => Enum.TryParse(Capitalise(t), out CommonExpenseType v) ? (CommonExpenseType?)v : null)
            .Where(v => v is not null).Select(v => v!.Value).ToList();
        if (types.Count == 0)
        {
            throw Fail("types", "Choose at least one expense type.");
        }

        return await db.Set<CommonExpense>()
            .Where(e => e.Status == CommonExpenseStatus.Active
                && e.AllocationRunId == null
                && e.Date >= request.PeriodFrom && e.Date <= request.PeriodTo
                && types.Contains(e.Type))
            .ToListAsync(ct);
    }

    private static Dictionary<long, decimal> Percentage(
        List<Project> projects, CommonExpenseAllocationRequest request, decimal total)
    {
        Dictionary<long, decimal> pct = (request.Shares ?? [])
            .ToDictionary(s => s.ProjectId, s => s.Value);
        var ordered = projects.Select(p => pct.GetValueOrDefault(p.Id, 0m)).ToList();
        try
        {
            IReadOnlyList<decimal> parts = AmountSplitter.SplitByPercentage(total, ordered);
            return Zip(projects, parts);
        }
        catch (ArgumentException ex)
        {
            throw Fail("percentages", ex.Message);
        }
    }

    private static Dictionary<long, decimal> Manual(
        List<Project> projects, CommonExpenseAllocationRequest request, decimal total)
    {
        Dictionary<long, decimal> amounts = (request.Shares ?? [])
            .ToDictionary(s => s.ProjectId, s => s.Value);
        decimal sum = Money.Round(amounts.Values.Sum());
        if (Math.Abs(sum - Money.Round(total)) > Money.Tolerance)
        {
            throw Fail("shares", $"Manual amounts total {sum:0.00}; they must equal the pool of {total:0.00}.");
        }

        return projects.ToDictionary(p => p.Id, p => Money.Round(amounts.GetValueOrDefault(p.Id, 0m)));
    }

    private static Dictionary<long, decimal> Zip(List<Project> projects, IReadOnlyList<decimal> parts) =>
        projects.Select((p, i) => (p.Id, parts[i])).ToDictionary(x => x.Id, x => x.Item2);

    private static decimal? PercentFor(CommonExpenseAllocationRequest request, long projectId) =>
        (request.Shares ?? []).FirstOrDefault(s => s.ProjectId == projectId)?.Value;

    private async Task<Dictionary<long, decimal>> ProjectCostsAsync(List<long> projectIds, CancellationToken ct)
    {
        var rows = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.ProjectId != null && projectIds.Contains(e.ProjectId.Value))
            .Join(db.ExpenseCategories.AsNoTracking(), e => e.CategoryId, c => c.Id,
                (e, c) => new { ProjectId = e.ProjectId!.Value, c.IsCost, Net = e.Debit - e.Credit })
            .Where(x => x.IsCost)
            .GroupBy(x => x.ProjectId)
            .Select(g => new { ProjectId = g.Key, Total = g.Sum(x => x.Net) })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.ProjectId, r => Money.Round(r.Total));
    }

    private async Task<CommonExpenseAllocationRunDto> ToDtoAsync(CommonExpenseAllocationRun run, CancellationToken ct)
    {
        List<long> projectIds = run.Lines.Select(l => l.ProjectId).Distinct().ToList();
        Dictionary<long, string> names = await db.Projects.AsNoTracking()
            .Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var lines = run.Lines
            .Select(l => new AllocationPreviewLineDto(
                l.ProjectId, names.GetValueOrDefault(l.ProjectId, ""), 0m, l.Amount, l.Amount, l.Percent))
            .OrderBy(l => l.ProjectName)
            .ToList();

        return new CommonExpenseAllocationRunDto(
            run.Id, run.PeriodFrom, run.PeriodTo, run.Types, run.Method, run.PoolAmount,
            run.Status.ToString(), run.Note, run.CreatedAtUtc, run.CreatedByUserId, lines);
    }

    private static decimal FloorPaisa(decimal v) => Math.Floor(v * 100m) / 100m;

    private static string Capitalise(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
