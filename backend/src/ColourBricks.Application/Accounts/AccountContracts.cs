using ColourBricks.Domain.Accounts;

namespace ColourBricks.Application.Accounts;

/// <summary><see cref="StatementBalance"/> — see <see cref="AccountDto"/>.</summary>
public sealed record AccountListItemDto(
    long Id,
    string Name,
    AccountType Type,
    string? BankName,
    string? AccountNumber,
    bool IsActive,
    decimal OpeningBalance = 0m,
    decimal StatementBalance = 0m);

/// <summary>
/// Account detail. <see cref="Balance"/> is derived (<c>OpeningBalance + Σcredit −
/// Σdebit</c> from the ledger, plan.md §5.3). <see cref="OpeningBalanceLocked"/> is
/// true once any ledger entry exists — the opening balance and its date can no
/// longer change (P1-T06 acceptance). <see cref="StatementBalance"/> is what the bank
/// itself says: <c>OpeningBalance + Σcredit − Σdebit</c> over every committed
/// statement row except excluded ones, reconciled or not (client request, 2026-09-24).
/// </summary>
public sealed record AccountDto(
    long Id,
    string Name,
    AccountType Type,
    string? BankName,
    string? AccountNumber,
    string? Ifsc,
    decimal OpeningBalance,
    DateOnly OpeningBalanceDate,
    decimal Balance,
    bool OpeningBalanceLocked,
    bool IsActive,
    string ConcurrencyStamp,
    decimal StatementBalance = 0m,
    decimal StatementCredits = 0m,
    decimal StatementDebits = 0m);

public sealed record CreateAccountRequest(
    string Name,
    AccountType Type,
    decimal OpeningBalance,
    DateOnly OpeningBalanceDate,
    string? BankName = null,
    string? AccountNumber = null,
    string? Ifsc = null);

public sealed record UpdateAccountRequest(
    string Name,
    AccountType Type,
    decimal OpeningBalance,
    DateOnly OpeningBalanceDate,
    bool IsActive,
    string ConcurrencyStamp,
    string? BankName = null,
    string? AccountNumber = null,
    string? Ifsc = null);

public sealed class AccountExactDuplicateException(long existingId, string name)
    : Exception($"An account named '{name}' already exists.")
{
    public long ExistingId { get; } = existingId;
}
