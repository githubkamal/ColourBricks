using ColourBricks.Application.Ledger;
using ColourBricks.Domain.Services;
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
            .Select(c => new ExpenseCategoryDto(
                c.Id, c.Name, c.Slug, c.Bucket, c.IsCost, c.IsActive, c.IsSystem, c.ConcurrencyStamp))
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

    public async Task<ExpenseCategoryDto> CreateAsync(
        CreateExpenseCategoryRequest request, CancellationToken cancellationToken)
    {
        string name = request.Name.Trim();
        string norm = NameNormalizer.Normalize(name);

        Domain.Ledger.ExpenseCategory? exact = await db.ExpenseCategories
            .FirstOrDefaultAsync(c => c.Name == name, cancellationToken);
        if (exact is not null)
        {
            throw new ExpenseCategoryExactDuplicateException(exact.Id, exact.Name);
        }

        string slug = await NextSlugAsync(norm, cancellationToken);
        var category = new Domain.Ledger.ExpenseCategory
        {
            Name = name,
            Slug = slug,
            Bucket = request.Bucket.Trim(),
            IsCost = request.IsCost,
            IsSystem = false,
            IsActive = true,
        };
        db.ExpenseCategories.Add(category);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            Domain.Ledger.ExpenseCategory? raced = await db.ExpenseCategories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Name == name, cancellationToken);
            throw new ExpenseCategoryExactDuplicateException(raced?.Id ?? 0, name);
        }

        return new ExpenseCategoryDto(
            category.Id, category.Name, category.Slug, category.Bucket, category.IsCost,
            category.IsActive, category.IsSystem, category.ConcurrencyStamp);
    }

    public async Task<ExpenseCategoryDto?> UpdateAsync(
        long id, UpdateExpenseCategoryRequest request, CancellationToken cancellationToken)
    {
        Domain.Ledger.ExpenseCategory? category = await db.ExpenseCategories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return null;
        }

        if (category.IsSystem)
        {
            throw new ExpenseCategoryIsSystemException(id);
        }

        string name = request.Name.Trim();
        db.Entry(category).Property(c => c.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;

        category.Name = name;
        category.IsActive = request.IsActive;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            Domain.Ledger.ExpenseCategory? clash = await db.ExpenseCategories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Name == name && c.Id != id, cancellationToken);
            throw new ExpenseCategoryExactDuplicateException(clash?.Id ?? 0, name);
        }

        return new ExpenseCategoryDto(
            category.Id, category.Name, category.Slug, category.Bucket, category.IsCost,
            category.IsActive, category.IsSystem, category.ConcurrencyStamp);
    }

    private async Task<string> NextSlugAsync(string normalizedName, CancellationToken cancellationToken)
    {
        string baseSlug = normalizedName.Replace(' ', '_');
        if (string.IsNullOrEmpty(baseSlug))
        {
            baseSlug = "category";
        }

        string slug = baseSlug;
        int suffix = 2;
        while (await db.ExpenseCategories.AsNoTracking().AnyAsync(c => c.Slug == slug, cancellationToken))
        {
            slug = $"{baseSlug}_{suffix}";
            suffix++;
        }

        return slug;
    }
}
