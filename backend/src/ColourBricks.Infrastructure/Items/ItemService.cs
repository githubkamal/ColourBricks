using ColourBricks.Application.Common.Pagination;
using ColourBricks.Application.Items;
using ColourBricks.Domain.Items;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Items;

public sealed class ItemService(AppDbContext db) : IItemService
{
    private const int MaxSearchResults = 20;

    public async Task<PagedResult<ItemSearchItem>> ListAsync(
        long? categoryId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<Item> query = db.Items.AsNoTracking();

        if (categoryId is { } cat)
        {
            query = query.Where(i => i.CategoryId == cat);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string norm = NameNormalizer.Normalize(search);
            query = query.Where(i => i.NormalisedName.Contains(norm) || i.Name.Contains(search));
        }

        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize < 1 ? 50 : pageSize, 1, 200);

        int totalCount = await query.CountAsync(cancellationToken);
        List<Item> rows = await query
            .OrderBy(i => i.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        Dictionary<long, string> categories = await CategoryNamesAsync(cancellationToken);
        return PagedResult<ItemSearchItem>.Create(
            rows.Select(i => ToSearchItem(i, categories)).ToList(), page, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<ItemSearchItem>> SearchAsync(
        string query, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit <= 0 ? MaxSearchResults : limit, 1, MaxSearchResults);
        string norm = NameNormalizer.Normalize(query);
        if (norm.Length == 0)
        {
            return [];
        }

        List<Item> matches = await db.Items.AsNoTracking()
            .Where(i => i.IsActive && i.NormalisedName.Contains(norm))
            .Take(200)
            .ToListAsync(cancellationToken);

        Dictionary<long, string> categories = await CategoryNamesAsync(cancellationToken);

        // Rank: exact, then prefix, then contains; then alphabetical.
        return matches
            .OrderBy(i => i.NormalisedName == norm ? 0
                : i.NormalisedName.StartsWith(norm, StringComparison.Ordinal) ? 1 : 2)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(i => ToSearchItem(i, categories))
            .ToList();
    }

    public async Task<ItemDto?> GetAsync(long id, CancellationToken cancellationToken)
    {
        Item? item = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        Dictionary<long, string> categories = await CategoryNamesAsync(cancellationToken);
        return ToDto(item, categories);
    }

    public async Task<CreateItemResult> CreateAsync(
        CreateItemRequest request, bool confirmed, CancellationToken cancellationToken)
    {
        string norm = NameNormalizer.Normalize(request.Name);
        string unit = await ResolveUnitAsync(request.Unit, cancellationToken);
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);

        Item? exact = await db.Items.FirstOrDefaultAsync(i => i.NormalisedName == norm, cancellationToken);
        if (exact is not null)
        {
            throw new ItemExactDuplicateException(exact.Id, exact.Name);
        }

        if (!confirmed)
        {
            Dictionary<long, string> categories = await CategoryNamesAsync(cancellationToken);
            List<ItemNearDuplicate> nearby = (await db.Items.AsNoTracking()
                    .Select(i => new { i.Id, i.Name, i.NormalisedName, i.CategoryId })
                    .ToListAsync(cancellationToken))
                .Where(i => NameNormalizer.AreNearDuplicates(norm, i.NormalisedName))
                .Select(i => new ItemNearDuplicate(
                    i.Id, i.Name, i.CategoryId is { } c && categories.TryGetValue(c, out string? n) ? n : null))
                .ToList();

            if (nearby.Count > 0)
            {
                return CreateItemResult.NeedsConfirmation(nearby);
            }
        }

        var item = new Item
        {
            Name = request.Name.Trim(),
            NormalisedName = norm,
            CategoryId = request.CategoryId,
            Unit = unit,
            DefaultRate = request.DefaultRate,
            TaxRate = request.TaxRate,
        };

        db.Items.Add(item);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            Item? raced = await db.Items.AsNoTracking()
                .FirstOrDefaultAsync(i => i.NormalisedName == norm, cancellationToken);
            throw new ItemExactDuplicateException(raced?.Id ?? 0, request.Name);
        }

        Dictionary<long, string> names = await CategoryNamesAsync(cancellationToken);
        return CreateItemResult.Created(ToDto(item, names));
    }

    public async Task<ItemDto?> UpdateAsync(
        long id, UpdateItemRequest request, CancellationToken cancellationToken)
    {
        Item? item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        string unit = await ResolveUnitAsync(request.Unit, cancellationToken);
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);

        db.Entry(item).Property(i => i.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;

        // Editing the master touches this row only — a purchase line copied its
        // rate/tax/unit when it was entered (plan.md §5.2, P1-T03 acceptance).
        item.Name = request.Name.Trim();
        item.NormalisedName = NameNormalizer.Normalize(request.Name);
        item.CategoryId = request.CategoryId;
        item.Unit = unit;
        item.DefaultRate = request.DefaultRate;
        item.TaxRate = request.TaxRate;
        item.IsActive = request.IsActive;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            Item? clash = await db.Items.AsNoTracking()
                .FirstOrDefaultAsync(i => i.NormalisedName == item.NormalisedName && i.Id != id, cancellationToken);
            throw new ItemExactDuplicateException(clash?.Id ?? 0, request.Name);
        }

        Dictionary<long, string> categories = await CategoryNamesAsync(cancellationToken);
        return ToDto(item, categories);
    }

    public async Task<IReadOnlyList<ItemCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        await db.ItemCategories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new ItemCategoryDto(c.Id, c.Name, c.IsActive, c.ConcurrencyStamp))
            .ToListAsync(cancellationToken);

    public async Task<ItemCategoryDto> CreateCategoryAsync(
        CreateItemCategoryRequest request, CancellationToken cancellationToken)
    {
        string norm = NameNormalizer.Normalize(request.Name);
        ItemCategory? exact = await db.ItemCategories
            .FirstOrDefaultAsync(c => c.NormalisedName == norm, cancellationToken);
        if (exact is not null)
        {
            throw new ItemCategoryExactDuplicateException(exact.Id, exact.Name);
        }

        var category = new ItemCategory { Name = request.Name.Trim(), NormalisedName = norm };
        db.ItemCategories.Add(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            ItemCategory? raced = await db.ItemCategories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.NormalisedName == norm, cancellationToken);
            throw new ItemCategoryExactDuplicateException(raced?.Id ?? 0, request.Name);
        }

        return new ItemCategoryDto(category.Id, category.Name, category.IsActive, category.ConcurrencyStamp);
    }

    public async Task<IReadOnlyList<UnitDto>> ListUnitsAsync(CancellationToken cancellationToken) =>
        await db.Units.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.SortOrder).ThenBy(u => u.Code)
            .Select(u => new UnitDto(u.Id, u.Code, u.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<UnitDto> CreateUnitAsync(CreateUnitRequest request, CancellationToken cancellationToken)
    {
        string norm = NameNormalizer.Normalize(request.Code);
        UnitOfMeasure? existing = await db.Units
            .FirstOrDefaultAsync(u => u.NormalisedCode == norm, cancellationToken);
        if (existing is not null)
        {
            existing.IsActive = true;
            existing.SortOrder = request.SortOrder;
            await db.SaveChangesAsync(cancellationToken);
            return new UnitDto(existing.Id, existing.Code, existing.SortOrder);
        }

        var unit = new UnitOfMeasure
        {
            Code = request.Code.Trim(),
            NormalisedCode = norm,
            SortOrder = request.SortOrder,
        };
        db.Units.Add(unit);
        await db.SaveChangesAsync(cancellationToken);
        return new UnitDto(unit.Id, unit.Code, unit.SortOrder);
    }

    private async Task<string> ResolveUnitAsync(string unit, CancellationToken cancellationToken)
    {
        string norm = NameNormalizer.Normalize(unit);
        UnitOfMeasure? match = await db.Units.AsNoTracking()
            .FirstOrDefaultAsync(u => u.IsActive && u.NormalisedCode == norm, cancellationToken);

        if (match is null)
        {
            throw new ValidationException(
                [new ValidationFailure("unit", $"'{unit}' is not a known unit. Add it to the unit master first.")]);
        }

        return match.Code;
    }

    private async Task EnsureCategoryExistsAsync(long? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is not { } id)
        {
            return;
        }

        bool exists = await db.ItemCategories.AsNoTracking().AnyAsync(c => c.Id == id, cancellationToken);
        if (!exists)
        {
            throw new ValidationException(
                [new ValidationFailure("categoryId", "The selected category does not exist.")]);
        }
    }

    private async Task<Dictionary<long, string>> CategoryNamesAsync(CancellationToken cancellationToken) =>
        await db.ItemCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

    private static ItemSearchItem ToSearchItem(Item i, IReadOnlyDictionary<long, string> categories) =>
        new(i.Id, i.Name, CategoryName(i.CategoryId, categories), i.Unit, i.DefaultRate, i.TaxRate);

    private static ItemDto ToDto(Item i, IReadOnlyDictionary<long, string> categories) =>
        new(i.Id, i.Name, i.CategoryId, CategoryName(i.CategoryId, categories),
            i.Unit, i.DefaultRate, i.TaxRate, i.IsActive, i.ConcurrencyStamp);

    private static string? CategoryName(long? categoryId, IReadOnlyDictionary<long, string> categories) =>
        categoryId is { } c && categories.TryGetValue(c, out string? name) ? name : null;
}
