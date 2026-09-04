using ColourBricks.Domain.Accounts;

namespace ColourBricks.Application.Accounts;

public interface IAccountService
{
    /// <param name="unmasked">
    /// When false, <c>AccountNumber</c> is masked to the last four digits (BRD §29).
    /// </param>
    Task<IReadOnlyList<AccountListItemDto>> ListAsync(
        AccountType? type, bool includeInactive, bool unmasked, CancellationToken cancellationToken);

    Task<AccountDto?> GetAsync(long id, bool unmasked, CancellationToken cancellationToken);

    Task<AccountDto> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Throws <see cref="FluentValidation.ValidationException"/> (→ 400) if the opening
    /// balance or its date is changed once a ledger entry exists for the account.
    /// </summary>
    Task<AccountDto?> UpdateAsync(long id, UpdateAccountRequest request, CancellationToken cancellationToken);
}
