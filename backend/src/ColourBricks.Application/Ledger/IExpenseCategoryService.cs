namespace ColourBricks.Application.Ledger;

public interface IExpenseCategoryService
{
    Task<IReadOnlyList<ExpenseCategoryDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    /// <summary>Resolves a category slug to its id; throws if the slug is unknown.</summary>
    Task<long> RequireIdAsync(string slug, CancellationToken cancellationToken);
}
