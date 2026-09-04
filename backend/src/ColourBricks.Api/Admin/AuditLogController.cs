using ColourBricks.Api.Authorization;
using ColourBricks.Application.Common.Pagination;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Api.Admin;

/// <summary>Admin-only audit-trail viewer (BRD §65, plan.md §10).</summary>
[ApiController]
[Route("api/v1/admin/audit-logs")]
public sealed class AuditLogController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [HasPermission("audit_trail.view")]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> List(
        [FromQuery] AuditLogQuery query, CancellationToken cancellationToken)
    {
        IQueryable<Infrastructure.Auditing.AuditLog> rows = db.AuditLogs.AsNoTracking();

        if (query.UserId is { } userId)
        {
            rows = rows.Where(a => a.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            rows = rows.Where(a => a.Module == query.Module);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            rows = rows.Where(a => a.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.RecordId))
        {
            rows = rows.Where(a => a.RecordId == query.RecordId);
        }

        if (query.DateFrom is { } from)
        {
            DateTimeOffset fromUtc = new(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            rows = rows.Where(a => a.TimestampUtc >= fromUtc);
        }

        if (query.DateTo is { } to)
        {
            DateTimeOffset toExclusive = new(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            rows = rows.Where(a => a.TimestampUtc < toExclusive);
        }

        int totalCount = await rows.CountAsync(cancellationToken);

        List<AuditLogDto> items = await rows
            .OrderByDescending(a => a.TimestampUtc)
            .ThenByDescending(a => a.Id)
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(a => new AuditLogDto(
                a.Id, a.UserId, a.TimestampUtc, a.Module, a.Action, a.EntityType,
                a.RecordId, a.OldValues, a.NewValues, a.Details))
            .ToListAsync(cancellationToken);

        return PagedResult<AuditLogDto>.Create(items, query.Page, query.PageSize, totalCount);
    }
}

public sealed record AuditLogDto(
    long Id,
    long? UserId,
    DateTimeOffset TimestampUtc,
    string Module,
    string Action,
    string? EntityType,
    string RecordId,
    string? OldValues,
    string? NewValues,
    string? Details);

public sealed class AuditLogQuery
{
    private const int MaxPageSize = 200;
    private readonly int _page = 1;
    private readonly int _pageSize = 50;

    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = Math.Clamp(value <= 0 ? 50 : value, 1, MaxPageSize);
    }

    public long? UserId { get; init; }
    public string? Module { get; init; }
    public string? Action { get; init; }
    public string? RecordId { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }

    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;
}
