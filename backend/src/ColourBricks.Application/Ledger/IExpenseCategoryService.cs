namespace ColourBricks.Application.Ledger;

public interface IExpenseCategoryService
{
    Task<IReadOnlyList<ExpenseCategoryDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    /// <summary>Resolves a category slug to its id; throws if the slug is unknown.</summary>
    Task<long> RequireIdAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Adds an admin-defined category (e.g. a material type). Not seeded/system-owned.</summary>
    Task<ExpenseCategoryDto> CreateAsync(CreateExpenseCategoryRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Renames or (de)activates a category. Returns null if the id does not exist;
    /// throws <see cref="ExpenseCategoryIsSystemException"/> for a seeded category.
    /// </summary>
    Task<ExpenseCategoryDto?> UpdateAsync(
        long id, UpdateExpenseCategoryRequest request, CancellationToken cancellationToken);
}
