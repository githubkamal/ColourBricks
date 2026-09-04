using ColourBricks.Application.Allocations;
using ColourBricks.Domain.Allocations;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Services;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Allocations;

public sealed class AllocationEngine(AppDbContext db) : IAllocationEngine
{
    private sealed record OpenObligation(long Id, long ProjectId, DateOnly Date, string? Reference, decimal Remaining);

    public async Task<AllocationProposalDto> ProposeAsync(
        long partyId, decimal amount, string method, CancellationToken cancellationToken)
    {
        List<OpenObligation> open = await LoadOpenObligationsAsync(partyId, cancellationToken);

        decimal remaining = Money.Round(amount);
        var byProject = new Dictionary<long, (decimal Before, decimal Allocated, long? ObligationId, string? Reference)>();

        foreach (OpenObligation o in open.OrderBy(o => o.Date).ThenBy(o => o.Id))
        {
            if (remaining <= 0m)
            {
                break;
            }

            decimal slice = Math.Min(remaining, o.Remaining);
            if (slice <= 0m)
            {
                continue;
            }

            remaining -= slice;

            if (byProject.TryGetValue(o.ProjectId, out var acc))
            {
                byProject[o.ProjectId] = (acc.Before + o.Remaining, acc.Allocated + slice,
                    acc.ObligationId is null && acc.Allocated == 0m ? o.Id : null, acc.Reference);
            }
            else
            {
                byProject[o.ProjectId] = (o.Remaining, slice, o.Id, o.Reference);
            }
        }

        Dictionary<long, string> projectNames = await ProjectNamesAsync(byProject.Keys, cancellationToken);

        var lines = byProject
            .Select(kv => new AllocationLineDto(
                kv.Key,
                projectNames.GetValueOrDefault(kv.Key, ""),
                kv.Value.ObligationId,
                kv.Value.Reference,
                kv.Value.Before,
                kv.Value.Allocated,
                kv.Value.Before - kv.Value.Allocated))
            .OrderBy(l => l.ProjectName)
            .ToList();

        decimal totalAllocated = lines.Sum(l => l.Allocated);
        return new AllocationProposalDto(
            partyId, Money.Round(amount), method, lines, totalAllocated, Money.Round(amount) - totalAllocated);
    }

    public async Task ApplyAsync(
        long settlementId, IReadOnlyList<AllocationInputDto> allocations, string method,
        CancellationToken cancellationToken)
    {
        Settlement settlement = await db.Settlements
            .FirstAsync(s => s.Id == settlementId, cancellationToken);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx =
            await db.Database.BeginTransactionAsync(cancellationToken);

        // Row-lock every open obligation for this party for the life of the transaction
        // so a concurrent payment sees these rows as taken.
        List<Obligation> locked = await db.Obligations
            .FromSqlRaw(
                "SELECT * FROM `Obligation` WHERE `PartyId` = {0} AND `Type` = {1} AND `Status` = {2} FOR UPDATE",
                settlement.PartyId!.Value, (byte)ObligationType.VendorPurchase, (byte)ObligationStatus.Active)
            .ToListAsync(cancellationToken);

        Dictionary<long, decimal> remainingById = await RemainingByObligationAsync(
            locked.Select(o => o.Id).ToList(), cancellationToken);

        decimal totalAllocated = 0m;
        foreach (AllocationInputDto input in allocations)
        {
            decimal available = input.ObligationId is { } oid
                ? remainingById.GetValueOrDefault(oid, 0m)
                : locked.Where(o => o.ProjectId == input.ProjectId).Sum(o => remainingById.GetValueOrDefault(o.Id, 0m));

            if (Money.Round(input.Amount) > Money.Round(available) + Money.Epsilon)
            {
                await tx.RollbackAsync(cancellationToken);
                throw new AllocationExceedsOutstandingException(
                    input.ObligationId ?? 0, input.Amount, available);
            }

            totalAllocated += input.Amount;
        }

        if (Money.Round(totalAllocated) > Money.Round(settlement.Amount) + Money.Epsilon)
        {
            await tx.RollbackAsync(cancellationToken);
            throw new AllocationSumMismatchException(totalAllocated, settlement.Amount);
        }

        foreach (AllocationInputDto input in allocations)
        {
            db.Allocations.Add(new Allocation
            {
                SettlementId = settlementId,
                ObligationId = input.ObligationId,
                ProjectId = input.ProjectId,
                PartyId = settlement.PartyId!.Value,
                Amount = Money.Round(input.Amount),
                Method = method,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private async Task<List<OpenObligation>> LoadOpenObligationsAsync(
        long partyId, CancellationToken cancellationToken)
    {
        var obligations = await db.Obligations.AsNoTracking()
            .Where(o => o.PartyId == partyId
                && o.Type == ObligationType.VendorPurchase
                && o.Status == ObligationStatus.Active)
            .Select(o => new { o.Id, o.ProjectId, o.Date, o.Reference, o.Amount })
            .ToListAsync(cancellationToken);

        Dictionary<long, decimal> remaining = await RemainingByObligationAsync(
            obligations.Select(o => o.Id).ToList(), cancellationToken, obligations.ToDictionary(o => o.Id, o => o.Amount));

        return obligations
            .Select(o => new OpenObligation(o.Id, o.ProjectId, o.Date, o.Reference, remaining.GetValueOrDefault(o.Id, 0m)))
            .Where(o => o.Remaining > 0m)
            .ToList();
    }

    private async Task<Dictionary<long, decimal>> RemainingByObligationAsync(
        List<long> obligationIds, CancellationToken cancellationToken,
        Dictionary<long, decimal>? amounts = null)
    {
        if (obligationIds.Count == 0)
        {
            return [];
        }

        amounts ??= await db.Obligations.AsNoTracking()
            .Where(o => obligationIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Amount, cancellationToken);

        Dictionary<long, decimal> allocated = (await db.Allocations.AsNoTracking()
                .Where(a => a.ObligationId != null && obligationIds.Contains(a.ObligationId.Value))
                // A null SettlementId is a non-cash advance application (P3-T05) — always counts;
                // otherwise the backing settlement must still be active.
                .Where(a => a.SettlementId == null
                    || db.Settlements.Any(s => s.Id == a.SettlementId && s.Status == SettlementStatus.Active))
                .GroupBy(a => a.ObligationId!.Value)
                .Select(g => new { ObligationId = g.Key, Sum = g.Sum(a => a.Amount) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.ObligationId, x => x.Sum);

        // Inline part-payments (P2-T03): obligation-linked settlements with no Allocation rows.
        List<long> allocatedSettlementIds = await db.Allocations.AsNoTracking()
            .Where(a => a.SettlementId != null)
            .Select(a => a.SettlementId!.Value).Distinct().ToListAsync(cancellationToken);

        Dictionary<long, decimal> inline = (await db.Settlements.AsNoTracking()
                .Where(s => s.ObligationId != null
                    && obligationIds.Contains(s.ObligationId.Value)
                    && s.Direction == SettlementDirection.Out
                    && s.Status == SettlementStatus.Active
                    && !allocatedSettlementIds.Contains(s.Id))
                .GroupBy(s => s.ObligationId!.Value)
                .Select(g => new { ObligationId = g.Key, Sum = g.Sum(s => s.Amount) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.ObligationId, x => x.Sum);

        var result = new Dictionary<long, decimal>();
        foreach (long id in obligationIds)
        {
            decimal amount = amounts.GetValueOrDefault(id, 0m);
            decimal paid = allocated.GetValueOrDefault(id, 0m) + inline.GetValueOrDefault(id, 0m);
            result[id] = Money.Round(amount - paid);
        }

        return result;
    }

    private async Task<Dictionary<long, string>> ProjectNamesAsync(
        IEnumerable<long> projectIds, CancellationToken cancellationToken)
    {
        List<long> ids = projectIds.Distinct().ToList();
        return await db.Projects.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);
    }
}
