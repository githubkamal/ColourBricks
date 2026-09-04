using ColourBricks.Application.Ledger;
using ColourBricks.Application.Payments;
using ColourBricks.Application.Receipts;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Receipts;

public sealed class ReceiptService(
    AppDbContext db,
    ILedgerPostingService ledger,
    IExpenseCategoryService categories,
    IPaymentModeService paymentModes) : IReceiptService
{
    private const string SourceType = "ProjectReceipt";

    public async Task<ReceiptDto> RecordAsync(
        RecordReceiptRequest request, CancellationToken cancellationToken)
    {
        bool projectExists = await db.Projects.AnyAsync(p => p.Id == request.ProjectId, cancellationToken);
        if (!projectExists)
        {
            throw new ValidationException([new ValidationFailure("projectId", "The project does not exist.")]);
        }

        await paymentModes.ValidateInstructionAsync(
            new PaymentInstruction(request.PaymentModeId, request.ReferenceNo, request.AccountId),
            cancellationToken);

        var settlement = new Settlement
        {
            Direction = SettlementDirection.In,
            ProjectId = request.ProjectId,
            IncomeType = request.Type,
            Date = request.Date,
            Amount = request.Amount,
            PaymentModeId = request.PaymentModeId,
            AccountId = request.AccountId,
            ReferenceNo = request.ReferenceNo,
            Description = request.Description,
            Status = SettlementStatus.Active,
        };
        db.Settlements.Add(settlement);
        await db.SaveChangesAsync(cancellationToken);

        long incomeCategory = await categories.RequireIdAsync("project_income", cancellationToken);
        var legs = new List<LedgerLeg>
        {
            // Money into the project's income (plan.md §5.3 — income = Σ(debit − credit)).
            new(incomeCategory, Debit: request.Amount, Credit: 0m, ProjectId: request.ProjectId),
        };
        if (request.AccountId is { } accountId)
        {
            // Cash into the account (plan.md §5.3 — balance = opening + credits − debits).
            legs.Add(new LedgerLeg(incomeCategory, Debit: 0m, Credit: request.Amount, AccountId: accountId));
        }

        await ledger.PostAsync(
            new LedgerPosting(SourceType, settlement.Id, request.Date, legs), cancellationToken);

        return (await GetAsync(settlement.Id, cancellationToken))!;
    }

    public async Task<IReadOnlyList<ReceiptDto>> ListAsync(
        long projectId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        IQueryable<Settlement> query = db.Settlements.AsNoTracking()
            .Where(s => s.ProjectId == projectId && s.Direction == SettlementDirection.In);

        if (from is { } f)
        {
            query = query.Where(s => s.Date >= f);
        }

        if (to is { } t)
        {
            query = query.Where(s => s.Date <= t);
        }

        List<Settlement> rows = await query.OrderByDescending(s => s.Date).ThenByDescending(s => s.Id)
            .ToListAsync(cancellationToken);
        return await ToDtosAsync(rows, cancellationToken);
    }

    public async Task<ReceiptDto?> GetAsync(long id, CancellationToken cancellationToken)
    {
        Settlement? settlement = await db.Settlements.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.Direction == SettlementDirection.In, cancellationToken);
        if (settlement is null)
        {
            return null;
        }

        return (await ToDtosAsync([settlement], cancellationToken))[0];
    }

    public async Task<bool> ReverseAsync(long id, string reason, CancellationToken cancellationToken)
    {
        Settlement? settlement = await db.Settlements
            .FirstOrDefaultAsync(s => s.Id == id && s.Direction == SettlementDirection.In, cancellationToken);
        if (settlement is null)
        {
            return false;
        }

        if (settlement.Status == SettlementStatus.Reversed)
        {
            throw new ValidationException([new ValidationFailure("id", "This receipt is already reversed.")]);
        }

        await ledger.ReverseAsync(SourceType, id, reason, cancellationToken);
        settlement.Status = SettlementStatus.Reversed;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<decimal> TotalActiveIncomeAsync(long projectId, CancellationToken cancellationToken) =>
        await db.Settlements.AsNoTracking()
            .Where(s => s.ProjectId == projectId
                && s.Direction == SettlementDirection.In
                && s.Status == SettlementStatus.Active)
            .SumAsync(s => (decimal?)s.Amount, cancellationToken) ?? 0m;

    private async Task<List<ReceiptDto>> ToDtosAsync(
        List<Settlement> rows, CancellationToken cancellationToken)
    {
        Dictionary<long, string> modes = await db.PaymentModes.AsNoTracking()
            .ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);
        Dictionary<long, string> accounts = await db.Accounts.AsNoTracking()
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        return rows.Select(s => new ReceiptDto(
            s.Id, s.ProjectId!.Value, s.IncomeType ?? IncomeType.Other, s.Date, s.Amount,
            s.PaymentModeId, modes.GetValueOrDefault(s.PaymentModeId, ""),
            s.AccountId, s.AccountId is { } a ? accounts.GetValueOrDefault(a) : null,
            s.ReferenceNo, s.Description, s.Status, s.ConcurrencyStamp)).ToList();
    }
}
