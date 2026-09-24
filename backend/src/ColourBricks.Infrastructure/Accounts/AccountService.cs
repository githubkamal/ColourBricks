using ColourBricks.Application.Accounts;
using ColourBricks.Application.Ledger;
using ColourBricks.Domain.Accounts;
using ColourBricks.Domain.Banking;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Accounts;

public sealed class AccountService(AppDbContext db, ILedgerQueryService ledger) : IAccountService
{
    public async Task<IReadOnlyList<AccountListItemDto>> ListAsync(
        AccountType? type, bool includeInactive, bool unmasked, CancellationToken cancellationToken)
    {
        IQueryable<Account> query = db.Accounts.AsNoTracking();
        if (type is { } t)
        {
            query = query.Where(a => a.Type == t);
        }

        if (!includeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        List<Account> rows = await query
            .OrderBy(a => a.Type).ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);

        List<long> ids = rows.Select(a => a.Id).ToList();
        Dictionary<long, (decimal Credit, decimal Debit)> statement =
            (await StatementRows(ids)
                .GroupBy(t => t.AccountId)
                .Select(g => new { g.Key, Credit = g.Sum(t => t.Credit), Debit = g.Sum(t => t.Debit) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Key, x => (x.Credit, x.Debit));

        return rows
            .Select(a =>
            {
                (decimal credit, decimal debit) = statement.GetValueOrDefault(a.Id);
                return new AccountListItemDto(
                    a.Id, a.Name, a.Type, a.BankName, Number(a.AccountNumber, unmasked), a.IsActive,
                    a.OpeningBalance, a.OpeningBalance + credit - debit);
            })
            .ToList();
    }

    public async Task<AccountDto?> GetAsync(long id, bool unmasked, CancellationToken cancellationToken)
    {
        Account? account = await db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return account is null ? null : await ToDtoAsync(account, unmasked, cancellationToken);
    }

    public async Task<AccountDto> CreateAsync(
        CreateAccountRequest request, CancellationToken cancellationToken)
    {
        string norm = NameNormalizer.Normalize(request.Name);

        Account? exact = await db.Accounts
            .FirstOrDefaultAsync(a => a.NormalisedName == norm, cancellationToken);
        if (exact is not null)
        {
            throw new AccountExactDuplicateException(exact.Id, exact.Name);
        }

        var account = new Account
        {
            Name = request.Name.Trim(),
            NormalisedName = norm,
            Type = request.Type,
            BankName = request.BankName,
            AccountNumber = request.AccountNumber,
            Ifsc = request.Ifsc,
            OpeningBalance = request.OpeningBalance,
            OpeningBalanceDate = request.OpeningBalanceDate,
        };

        db.Accounts.Add(account);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            Account? raced = await db.Accounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.NormalisedName == norm, cancellationToken);
            throw new AccountExactDuplicateException(raced?.Id ?? 0, request.Name);
        }

        return await ToDtoAsync(account, unmasked: true, cancellationToken);
    }

    public async Task<AccountDto?> UpdateAsync(
        long id, UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        Account? account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null)
        {
            return null;
        }

        bool hasLedger = await db.LedgerEntries.AnyAsync(l => l.AccountId == id, cancellationToken);
        bool openingChanged =
            request.OpeningBalance != account.OpeningBalance
            || request.OpeningBalanceDate != account.OpeningBalanceDate;

        if (hasLedger && openingChanged)
        {
            throw new ValidationException([
                new ValidationFailure(
                    "openingBalance",
                    "The opening balance and its date cannot change once the account has transactions.")
            ]);
        }

        string norm = NameNormalizer.Normalize(request.Name);
        db.Entry(account).Property(a => a.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;

        account.Name = request.Name.Trim();
        account.NormalisedName = norm;
        account.Type = request.Type;
        account.BankName = request.BankName;
        account.AccountNumber = request.AccountNumber;
        account.Ifsc = request.Ifsc;
        account.OpeningBalance = request.OpeningBalance;
        account.OpeningBalanceDate = request.OpeningBalanceDate;
        account.IsActive = request.IsActive;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            Account? clash = await db.Accounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.NormalisedName == norm && a.Id != id, cancellationToken);
            throw new AccountExactDuplicateException(clash?.Id ?? 0, request.Name);
        }

        return await ToDtoAsync(account, unmasked: true, cancellationToken);
    }

    private async Task<AccountDto> ToDtoAsync(Account a, bool unmasked, CancellationToken cancellationToken)
    {
        // plan.md §5.3 — balance is derived from the ledger, never stored.
        decimal balance = await ledger.GetAccountBalanceAsync(a.Id, cancellationToken);
        bool locked = await db.LedgerEntries.AnyAsync(l => l.AccountId == a.Id, cancellationToken);

        var totals = await StatementRows([a.Id])
            .GroupBy(_ => 1)
            .Select(g => new { Credit = g.Sum(t => t.Credit), Debit = g.Sum(t => t.Debit) })
            .FirstOrDefaultAsync(cancellationToken);
        decimal credits = totals?.Credit ?? 0m;
        decimal debits = totals?.Debit ?? 0m;

        return new AccountDto(
            a.Id, a.Name, a.Type, a.BankName, Number(a.AccountNumber, unmasked), a.Ifsc,
            a.OpeningBalance, a.OpeningBalanceDate, balance, locked, a.IsActive, a.ConcurrencyStamp,
            a.OpeningBalance + credits - debits, credits, debits);
    }

    /// <summary>
    /// Committed statement rows that count toward the bank-side balance. Excluded rows are
    /// the accountant saying "this line should not count", so they are left out; every
    /// other status — pending, on hold, reconciled, internal transfer — is real money the
    /// bank moved. Derived, never stored (plan.md §5.3).
    /// </summary>
    private IQueryable<BankTransaction> StatementRows(List<long> accountIds) =>
        db.BankTransactions.AsNoTracking()
            .Where(t => accountIds.Contains(t.AccountId) && t.Status != BankTransactionStatus.Excluded);

    private static string? Number(string? accountNumber, bool unmasked)
    {
        if (unmasked || string.IsNullOrEmpty(accountNumber))
        {
            return accountNumber;
        }

        return accountNumber.Length <= 4
            ? new string('*', accountNumber.Length)
            : string.Concat(new string('*', accountNumber.Length - 4), accountNumber.AsSpan(accountNumber.Length - 4));
    }
}
