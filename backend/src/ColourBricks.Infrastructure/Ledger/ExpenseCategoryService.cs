using ColourBricks.Application.Ledger;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Ledger;

public sealed class ExpenseCategoryService(AppDbContext db) : IExpenseCategoryService
{
    public async Task<IReadOnlyList<ExpenseCategoryDto>> ListAsync(
        bool includeInactive, CancellationToken cancellationToken)
    {
        IQueryable<Domain.Ledger.ExpenseCategory> query = db.ExpenseCategories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Bucket).ThenBy(c => c.Name)
            .Select(c => new ExpenseCategoryDto(c.Id, c.Name, c.Slug, c.Bucket, c.IsCost, c.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<long> RequireIdAsync(string slug, CancellationToken cancellationToken)
    {
        long id = await db.ExpenseCategories.AsNoTracking()
            .Where(c => c.Slug == slug)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return id == 0
            ? throw new InvalidOperationException($"Expense category '{slug}' is not seeded.")
            : id;
    }
}
