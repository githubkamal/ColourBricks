using ColourBricks.Application.Ledger;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Ledger;

public sealed class LedgerQueryService(AppDbContext db) : ILedgerQueryService
{
    public async Task<decimal> GetAccountBalanceAsync(long accountId, CancellationToken cancellationToken)
    {
        decimal opening = await db.Accounts.AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => a.OpeningBalance)
            .FirstOrDefaultAsync(cancellationToken);

        var totals = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .GroupBy(_ => 1)
            .Select(g => new { Credit = g.Sum(x => x.Credit), Debit = g.Sum(x => x.Debit) })
            .FirstOrDefaultAsync(cancellationToken);

        return opening + (totals?.Credit ?? 0m) - (totals?.Debit ?? 0m);
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetProjectCostByBucketAsync(
        long projectId, CancellationToken cancellationToken)
    {
        var rows = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .Join(db.ExpenseCategories.AsNoTracking(),
                e => e.CategoryId, c => c.Id,
                (e, c) => new { c.Bucket, c.IsCost, Net = e.Debit - e.Credit })
            .Where(x => x.IsCost)
            .GroupBy(x => x.Bucket)
            .Select(g => new { Bucket = g.Key, Total = g.Sum(x => x.Net) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Bucket, r => r.Total);
    }

    public async Task<decimal> GetProjectActualCostAsync(long projectId, CancellationToken cancellationToken)
    {
        return await db.LedgerEntries.AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .Join(db.ExpenseCategories.AsNoTracking(),
                e => e.CategoryId, c => c.Id,
                (e, c) => new { c.IsCost, Net = e.Debit - e.Credit })
            .Where(x => x.IsCost)
            .SumAsync(x => (decimal?)x.Net, cancellationToken) ?? 0m;
    }

    public async Task<IReadOnlyDictionary<long, decimal>> GetProjectCostByCategoryAsync(
        long projectId, CancellationToken cancellationToken)
    {
        var rows = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .Join(db.ExpenseCategories.AsNoTracking(),
                e => e.CategoryId, c => c.Id,
                (e, c) => new { e.CategoryId, c.IsCost, Net = e.Debit - e.Credit })
            .Where(x => x.IsCost)
            .GroupBy(x => x.CategoryId)
            .Select(g => new { CategoryId = g.Key, Total = g.Sum(x => x.Net) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.CategoryId, r => r.Total);
    }

    public async Task<decimal> GetProjectIncomeAsync(long projectId, CancellationToken cancellationToken)
    {
        // Only the income bucket — the liability ("*_payable") categories are also
        // IsCost = false but must not be read as income.
        return await db.LedgerEntries.AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .Join(db.ExpenseCategories.AsNoTracking(),
                e => e.CategoryId, c => c.Id,
                (e, c) => new { c.Bucket, Net = e.Debit - e.Credit })
            .Where(x => x.Bucket == "Income")
            .SumAsync(x => (decimal?)x.Net, cancellationToken) ?? 0m;
    }

    public async Task<decimal> GetPartyPayableBalanceAsync(
        long partyId, long categoryId, CancellationToken cancellationToken)
    {
        return await db.LedgerEntries.AsNoTracking()
            .Where(e => e.PartyId == partyId && e.CategoryId == categoryId)
            .SumAsync(e => (decimal?)(e.Credit - e.Debit), cancellationToken) ?? 0m;
    }

    public async Task<decimal> GetPartyPayableBalanceForProjectAsync(
        long partyId, long projectId, long categoryId, CancellationToken cancellationToken)
    {
        return await db.LedgerEntries.AsNoTracking()
            .Where(e => e.PartyId == partyId && e.ProjectId == projectId && e.CategoryId == categoryId)
            .SumAsync(e => (decimal?)(e.Credit - e.Debit), cancellationToken) ?? 0m;
    }

    public async Task<decimal> GetVendorAdvanceBalanceAsync(
        long partyId, long categoryId, CancellationToken cancellationToken)
    {
        decimal net = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.PartyId == partyId && e.ProjectId == null && e.CategoryId == categoryId)
            .SumAsync(e => (decimal?)(e.Credit - e.Debit), cancellationToken) ?? 0m;

        return net < 0m ? -net : 0m;
    }

    public async Task<IReadOnlyList<ProjectLedgerRowDto>> GetProjectLedgerAsync(
        long projectId, CancellationToken cancellationToken)
    {
        var rows = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .Join(db.ExpenseCategories.AsNoTracking(),
                e => e.CategoryId, c => c.Id,
                (e, c) => new
                {
                    e.Id,
                    e.EntryDate,
                    e.CategoryId,
                    c.Name,
                    c.Bucket,
                    e.AccountId,
                    e.PartyId,
                    e.Debit,
                    e.Credit,
                    e.SourceType,
                    e.SourceId,
                    e.IsReversal,
                })
            .OrderBy(r => r.EntryDate).ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ProjectLedgerRowDto(
            r.Id, r.EntryDate, r.CategoryId, r.Name, r.Bucket, r.AccountId, r.PartyId,
            r.Debit, r.Credit, r.SourceType, r.SourceId, r.IsReversal)).ToList();
    }
}
