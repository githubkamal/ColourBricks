using ColourBricks.Application.Allocations;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Allocations;

/// <summary>
/// Read side for allocation history (BRD §23) and the Vendor Payment Allocation
/// Report (BRD §51). Every figure comes from the persisted <c>Allocation</c> rows —
/// nothing is recomputed — so a reversed payment still shows its distribution,
/// flagged by the settlement's status.
/// </summary>
public sealed class AllocationHistoryService(AppDbContext db) : IAllocationHistoryService
{
    internal const string MultiProjectDescription = "Vendor payment (multi-project)";
    private const string AdvanceProjectLabel = "— Vendor advance —";

    public async Task<SettlementAllocationHistoryDto?> ForSettlementAsync(
        long settlementId, CancellationToken cancellationToken)
    {
        Settlement? settlement = await db.Settlements.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == settlementId, cancellationToken);
        if (settlement is null || settlement.PartyId is null)
        {
            return null;
        }

        string vendorName = await db.Parties.AsNoTracking()
            .Where(p => p.Id == settlement.PartyId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "";

        List<AllocationHistoryLineDto> lines = await LinesForSettlementsAsync(
            [settlementId], cancellationToken);

        return new SettlementAllocationHistoryDto(
            settlement.Id, settlement.Date, settlement.PartyId.Value, vendorName,
            settlement.Amount, settlement.Status.ToString(), lines);
    }

    public async Task<IReadOnlyList<VendorPaymentAllocationReportRowDto>> ReportAsync(
        VendorPaymentAllocationReportQuery query, CancellationToken cancellationToken)
    {
        IQueryable<Settlement> settlements = db.Settlements.AsNoTracking()
            .Where(s => s.Direction == SettlementDirection.Out
                && s.Description == MultiProjectDescription
                && s.PartyId != null);

        if (query.VendorId is { } vendorId)
        {
            settlements = settlements.Where(s => s.PartyId == vendorId);
        }

        if (query.DateFrom is { } from)
        {
            settlements = settlements.Where(s => s.Date >= from);
        }

        if (query.DateTo is { } to)
        {
            settlements = settlements.Where(s => s.Date <= to);
        }

        var headers = await settlements
            .Select(s => new { s.Id, s.Date, VendorId = s.PartyId!.Value, s.Amount, s.Status })
            .ToListAsync(cancellationToken);
        if (headers.Count == 0)
        {
            return [];
        }

        List<long> settlementIds = headers.Select(h => h.Id).ToList();

        Dictionary<long, string> vendorNames = await db.Parties.AsNoTracking()
            .Where(p => headers.Select(h => h.VendorId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        List<(long SettlementId, AllocationHistoryLineDto Line)> lines =
            await LinesWithSettlementAsync(settlementIds, cancellationToken);

        var headerById = headers.ToDictionary(h => h.Id);

        IEnumerable<VendorPaymentAllocationReportRowDto> rows = lines
            .Where(x => query.ProjectId is not { } pid || x.Line.ProjectId == pid)
            .Select(x =>
            {
                var h = headerById[x.SettlementId];
                return new VendorPaymentAllocationReportRowDto(
                    h.Date, h.VendorId, vendorNames.GetValueOrDefault(h.VendorId, ""),
                    x.SettlementId, h.Amount, x.Line.ProjectId, x.Line.ProjectName,
                    x.Line.Amount, x.Line.Method, h.Status.ToString());
            })
            .OrderBy(r => r.Date).ThenBy(r => r.SettlementId).ThenBy(r => r.ProjectName);

        return rows.ToList();
    }

    private async Task<List<AllocationHistoryLineDto>> LinesForSettlementsAsync(
        List<long> settlementIds, CancellationToken cancellationToken) =>
        (await LinesWithSettlementAsync(settlementIds, cancellationToken))
        .Select(x => x.Line)
        .ToList();

    private async Task<List<(long SettlementId, AllocationHistoryLineDto Line)>> LinesWithSettlementAsync(
        List<long> settlementIds, CancellationToken cancellationToken)
    {
        var raw = await db.Allocations.AsNoTracking()
            .Where(a => a.SettlementId != null && settlementIds.Contains(a.SettlementId.Value))
            .Select(a => new
            {
                SettlementId = a.SettlementId!.Value,
                a.Id,
                a.ProjectId,
                a.ObligationId,
                a.Amount,
                a.Method,
            })
            .ToListAsync(cancellationToken);

        List<long> projectIds = raw.Where(r => r.ProjectId != null)
            .Select(r => r.ProjectId!.Value).Distinct().ToList();
        Dictionary<long, string> projectNames = await db.Projects.AsNoTracking()
            .Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        List<long> obligationIds = raw.Where(r => r.ObligationId != null)
            .Select(r => r.ObligationId!.Value).Distinct().ToList();
        Dictionary<long, string?> obligationRefs = (await db.Obligations.AsNoTracking()
                .Where(o => obligationIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Reference })
                .ToListAsync(cancellationToken))
            .ToDictionary(o => o.Id, o => o.Reference);

        return raw
            .OrderBy(r => r.ProjectId == null).ThenBy(r => r.Id)
            .Select(r => (r.SettlementId, new AllocationHistoryLineDto(
                r.ProjectId,
                r.ProjectId is { } pid ? projectNames.GetValueOrDefault(pid, "") : AdvanceProjectLabel,
                r.ObligationId,
                r.ObligationId is { } oid ? obligationRefs.GetValueOrDefault(oid) : null,
                r.Amount,
                r.Method)))
            .ToList();
    }
}
