using ColourBricks.Application.Common.Pagination;

namespace ColourBricks.Application.Items;

public interface IItemService
{
    Task<PagedResult<ItemSearchItem>> ListAsync(
        long? categoryId, string? search, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Autocomplete: ranked (exact, then prefix, then contains), capped at 20.</summary>
    Task<IReadOnlyList<ItemSearchItem>> SearchAsync(
        string query, int limit, CancellationToken cancellationToken);

    Task<ItemDto?> GetAsync(long id, CancellationToken cancellationToken);

    Task<CreateItemResult> CreateAsync(
        CreateItemRequest request, bool confirmed, CancellationToken cancellationToken);

    Task<ItemDto?> UpdateAsync(long id, UpdateItemRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ItemCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken);

    Task<ItemCategoryDto> CreateCategoryAsync(
        CreateItemCategoryRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<UnitDto>> ListUnitsAsync(CancellationToken cancellationToken);

    Task<UnitDto> CreateUnitAsync(CreateUnitRequest request, CancellationToken cancellationToken);
}
