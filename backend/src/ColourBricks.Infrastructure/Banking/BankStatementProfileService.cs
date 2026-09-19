using ColourBricks.Application.Banking;
using ColourBricks.Domain.Banking;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Banking;

public sealed class BankStatementProfileService(AppDbContext db) : IBankStatementProfileService
{
    public async Task<BankStatementProfileDto> CreateAsync(
        CreateBankStatementProfileRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Accounts.AnyAsync(a => a.Id == request.AccountId, cancellationToken))
        {
            throw Fail("accountId", "The account does not exist.");
        }

        var profile = new BankStatementProfile { AccountId = request.AccountId };
        ApplyMapping(profile, request.Name, request.HeaderRowIndex, request.Delimiter, request.DateColumn,
            request.NarrationColumn, request.ReferenceColumn, request.BalanceColumn, request.SingleAmountColumn,
            request.AmountColumn, request.DebitColumn, request.CreditColumn, request.DebitSign,
            request.DateFormats);

        db.BankStatementProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(profile);
    }

    public async Task<IReadOnlyList<BankStatementProfileDto>> ListForAccountAsync(
        long accountId, CancellationToken cancellationToken) =>
        (await db.BankStatementProfiles.AsNoTracking()
            .Where(p => p.AccountId == accountId)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken))
        .Select(ToDto)
        .ToList();

    public async Task<BankStatementProfileDto?> GetAsync(long id, CancellationToken cancellationToken)
    {
        BankStatementProfile? p = await db.BankStatementProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return p is null ? null : ToDto(p);
    }

    public async Task<BankStatementProfileDto?> UpdateAsync(
        long id, UpdateBankStatementProfileRequest request, CancellationToken cancellationToken)
    {
        BankStatementProfile? profile = await db.BankStatementProfiles
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        ApplyMapping(profile, request.Name, request.HeaderRowIndex, request.Delimiter, request.DateColumn,
            request.NarrationColumn, request.ReferenceColumn, request.BalanceColumn, request.SingleAmountColumn,
            request.AmountColumn, request.DebitColumn, request.CreditColumn, request.DebitSign,
            request.DateFormats);

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(profile);
    }

    private static void ApplyMapping(
        BankStatementProfile profile, string name, int headerRowIndex, string delimiter, int dateColumn,
        int narrationColumn, int? referenceColumn, int? balanceColumn, bool singleAmountColumn,
        int? amountColumn, int? debitColumn, int? creditColumn, string debitSign, string dateFormats)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw Fail("name", "Give the profile a name.");
        }

        if (singleAmountColumn)
        {
            if (amountColumn is null)
            {
                throw Fail("amountColumn", "A single-amount-column profile needs an amount column.");
            }
        }
        else if (debitColumn is null || creditColumn is null)
        {
            throw Fail("debitColumn", "A two-column profile needs both a debit and a credit column.");
        }

        if (string.IsNullOrWhiteSpace(dateFormats))
        {
            throw Fail("dateFormats", "Give at least one date format, e.g. dd/MM/yyyy.");
        }

        profile.Name = name.Trim();
        profile.HeaderRowIndex = Math.Max(0, headerRowIndex);
        profile.Delimiter = string.IsNullOrEmpty(delimiter) ? "," : delimiter[..1];
        profile.DateColumn = dateColumn;
        profile.NarrationColumn = narrationColumn;
        profile.ReferenceColumn = referenceColumn;
        profile.BalanceColumn = balanceColumn;
        profile.SingleAmountColumn = singleAmountColumn;
        profile.AmountColumn = singleAmountColumn ? amountColumn : null;
        profile.DebitColumn = singleAmountColumn ? null : debitColumn;
        profile.CreditColumn = singleAmountColumn ? null : creditColumn;
        profile.DebitSign = string.Equals(debitSign, "Positive", StringComparison.OrdinalIgnoreCase)
            ? AmountSign.Positive
            : AmountSign.Negative;
        profile.DateFormats = dateFormats.Trim();
    }

    internal static BankStatementProfileDto ToDto(BankStatementProfile p) => new(
        p.Id, p.AccountId, p.Name, p.HeaderRowIndex, p.Delimiter, p.DateColumn, p.NarrationColumn,
        p.ReferenceColumn, p.BalanceColumn, p.SingleAmountColumn, p.AmountColumn, p.DebitColumn,
        p.CreditColumn, p.DebitSign.ToString(), p.DateFormats);

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
