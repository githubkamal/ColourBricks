using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Auth;
using ColourBricks.Application.Reporting.Framework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace ColourBricks.Infrastructure.Reporting.Framework;

/// <summary>
/// P8-T01 — materialises a <see cref="ReportPlan{TRow}"/> with EF Core: total count
/// and every column total over the <b>filtered</b> query (not the page), then the
/// requested page of rows. Resolves project scope and "today" once per run.
/// </summary>
public sealed class ReportExecutor(IProjectScopeFilter scopeFilter, TimeProvider clock)
{
    public async Task<ReportResultDto> RunAsync<TRow>(
        ReportDefinition<TRow> definition,
        IQueryable<TRow> source,
        ReportFilter filter,
        CancellationToken ct,
        bool unpaged = false)
        where TRow : IReportRow
    {
        ProjectScope scope = await scopeFilter.GetScopeAsync(ct);
        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        ReportPlan<TRow> plan = ReportPlanner.Plan(source, definition, filter, scope, today, unpaged);

        bool ef = plan.Filtered.Provider is IAsyncQueryProvider;

        int totalCount = ef ? await plan.Filtered.CountAsync(ct) : plan.Filtered.Count();

        var totals = new Dictionary<string, decimal>(definition.Aggregates.Count);
        foreach (ReportAggregate<TRow> aggregate in definition.Aggregates)
        {
            totals[aggregate.Key] = ef
                ? await plan.Filtered.SumAsync(aggregate.Selector, ct)
                : plan.Filtered.Sum(aggregate.Selector.Compile());
        }

        List<TRow> rows = ef ? await plan.Page.ToListAsync(ct) : plan.Page.ToList();

        int totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)plan.PageSize);

        return new ReportResultDto(
            definition.Key,
            definition.Title,
            definition.Columns,
            rows.Cast<object>().ToList(),
            totals,
            plan.ResolvedRange.From,
            plan.ResolvedRange.To,
            plan.PageNumber,
            plan.PageSize,
            totalCount,
            totalPages);
    }
}
