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

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw Fail("name", "Give the profile a name.");
        }

        AmountSign sign = string.Equals(request.DebitSign, "Positive", StringComparison.OrdinalIgnoreCase)
            ? AmountSign.Positive
            : AmountSign.Negative;

        if (request.SingleAmountColumn)
        {
            if (request.AmountColumn is null)
            {
                throw Fail("amountColumn", "A single-amount-column profile needs an amount column.");
            }
        }
        else if (request.DebitColumn is null || request.CreditColumn is null)
        {
            throw Fail("debitColumn", "A two-column profile needs both a debit and a credit column.");
        }

        if (string.IsNullOrWhiteSpace(request.DateFormats))
        {
            throw Fail("dateFormats", "Give at least one date format, e.g. dd/MM/yyyy.");
        }

        var profile = new BankStatementProfile
        {
            AccountId = request.AccountId,
            Name = request.Name.Trim(),
            HeaderRowIndex = Math.Max(0, request.HeaderRowIndex),
            Delimiter = string.IsNullOrEmpty(request.Delimiter) ? "," : request.Delimiter[..1],
            DateColumn = request.DateColumn,
            NarrationColumn = request.NarrationColumn,
            ReferenceColumn = request.ReferenceColumn,
            BalanceColumn = request.BalanceColumn,
            SingleAmountColumn = request.SingleAmountColumn,
            AmountColumn = request.SingleAmountColumn ? request.AmountColumn : null,
            DebitColumn = request.SingleAmountColumn ? null : request.DebitColumn,
            CreditColumn = request.SingleAmountColumn ? null : request.CreditColumn,
            DebitSign = sign,
            DateFormats = request.DateFormats.Trim(),
        };

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

    internal static BankStatementProfileDto ToDto(BankStatementProfile p) => new(
        p.Id, p.AccountId, p.Name, p.HeaderRowIndex, p.Delimiter, p.DateColumn, p.NarrationColumn,
        p.ReferenceColumn, p.BalanceColumn, p.SingleAmountColumn, p.AmountColumn, p.DebitColumn,
        p.CreditColumn, p.DebitSign.ToString(), p.DateFormats);

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
