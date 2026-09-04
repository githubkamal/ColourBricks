namespace ColourBricks.Application.Items;

public sealed record ItemDto(
    long Id,
    string Name,
    long? CategoryId,
    string? CategoryName,
    string Unit,
    decimal DefaultRate,
    decimal TaxRate,
    bool IsActive,
    string ConcurrencyStamp);

/// <summary>
/// One autocomplete hit. Carries the fields a purchase line prefills (BRD §14, §16)
/// so the picker needs a single call to fill unit, rate and tax.
/// </summary>
public sealed record ItemSearchItem(
    long Id,
    string Name,
    string? CategoryName,
    string Unit,
    decimal DefaultRate,
    decimal TaxRate);

/// <summary>An item close enough to a new name to warrant a warning (plan.md §6).</summary>
public sealed record ItemNearDuplicate(long Id, string Name, string? CategoryName);

public sealed record ItemCategoryDto(long Id, string Name, bool IsActive, string ConcurrencyStamp);

public sealed record UnitDto(long Id, string Code, int SortOrder);

public sealed record CreateItemRequest(
    string Name,
    string Unit,
    decimal DefaultRate,
    decimal TaxRate,
    long? CategoryId = null);

public sealed record UpdateItemRequest(
    string Name,
    string Unit,
    decimal DefaultRate,
    decimal TaxRate,
    bool IsActive,
    string ConcurrencyStamp,
    long? CategoryId = null);

public sealed record CreateItemCategoryRequest(string Name);

public sealed record CreateUnitRequest(string Code, int SortOrder = 100);

/// <summary>
/// Either a created item, or (when a near-duplicate exists and the caller has not
/// confirmed) the list of possible duplicates to show the user (BRD §70 rule 17).
/// </summary>
public sealed record CreateItemResult(ItemDto? Item, IReadOnlyList<ItemNearDuplicate> NearDuplicates)
{
    public bool RequiresConfirmation => Item is null;

    public static CreateItemResult Created(ItemDto item) => new(item, []);

    public static CreateItemResult NeedsConfirmation(IReadOnlyList<ItemNearDuplicate> nearDuplicates) =>
        new(null, nearDuplicates);
}

public sealed class ItemExactDuplicateException(long existingId, string name)
    : Exception($"An item named '{name}' already exists.")
{
    public long ExistingId { get; } = existingId;
}

public sealed class ItemCategoryExactDuplicateException(long existingId, string name)
    : Exception($"A category named '{name}' already exists.")
{
    public long ExistingId { get; } = existingId;
}
