using ColourBricks.Application.DirectExpenses;
using ColourBricks.Application.Ledger;
using ColourBricks.Application.Payments;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.DirectExpenses;

public sealed class DirectExpenseService(
    AppDbContext db,
    ILedgerPostingService ledger,
    IPaymentModeService paymentModes) : IDirectExpenseService
{
    private const string SourceType = "DirectExpense";

    public async Task<DirectExpenseDto> RecordAsync(
        RecordDirectExpenseRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == request.ProjectId, cancellationToken))
        {
            throw Fail("projectId", "The project does not exist.");
        }

        var category = await db.ExpenseCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category is null || !category.IsCost)
        {
            throw Fail("categoryId", "Choose a valid expense category.");
        }

        if (request.PaidImmediately)
        {
            await paymentModes.ValidateInstructionAsync(
                new PaymentInstruction(request.PaymentModeId!.Value, request.ReferenceNo, request.AccountId),
                cancellationToken);
        }

        var obligation = new Obligation
        {
            Type = ObligationType.DirectExpense,
            ProjectId = request.ProjectId,
            PartyId = request.PartyId,
            Date = request.Date,
            Amount = request.Amount,
            Description = request.Description,
            CategoryId = request.CategoryId,
            Status = ObligationStatus.Active,
        };
        db.Obligations.Add(obligation);

        // A reconciliation anchor only, no ledger legs of its own (client request,
        // 2026-09-06) — lets an immediately-paid expense optionally be linked to a
        // bank transaction via the existing reconcile-debit flow. The real posting
        // below is unchanged; Project stays required for a Direct Expense either way.
        Settlement? anchor = null;
        if (request.PaidImmediately && request.AccountId is { } anchorAccountId)
        {
            anchor = new Settlement
            {
                Direction = SettlementDirection.Out,
                ProjectId = request.ProjectId,
                PartyId = request.PartyId,
                Date = request.Date,
                Amount = request.Amount,
                PaymentModeId = request.PaymentModeId!.Value,
                AccountId = anchorAccountId,
                ReferenceNo = request.ReferenceNo,
                Description = "Direct expense (reconciliation anchor)",
                Status = SettlementStatus.Active,
            };
            db.Settlements.Add(anchor);
        }

        await db.SaveChangesAsync(cancellationToken);

        var legs = new List<LedgerLeg>
        {
            new(request.CategoryId, Debit: request.Amount, Credit: 0m,
                ProjectId: request.ProjectId, PartyId: request.PartyId),
        };
        if (request.PaidImmediately && request.AccountId is { } accountId)
        {
            // Immediate payment moves cash out of the account (plan.md §5.3).
            legs.Add(new LedgerLeg(request.CategoryId, Debit: request.Amount, Credit: 0m, AccountId: accountId));
        }

        await ledger.PostAsync(
            new LedgerPosting(SourceType, obligation.Id, request.Date, legs), cancellationToken);

        DirectExpenseDto dto = (await ListAsync(request.ProjectId, cancellationToken))
            .First(e => e.Id == obligation.Id);
        return dto with { SettlementId = anchor?.Id };
    }

    public async Task<IReadOnlyList<DirectExpenseDto>> ListAsync(
        long projectId, CancellationToken cancellationToken)
    {
        List<Obligation> rows = await db.Obligations.AsNoTracking()
            .Where(o => o.Type == ObligationType.DirectExpense && o.ProjectId == projectId)
            .OrderByDescending(o => o.Date).ThenByDescending(o => o.Id)
            .ToListAsync(cancellationToken);

        Dictionary<long, (string Name, string Bucket)> categories = await db.ExpenseCategories.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => (c.Name, c.Bucket), cancellationToken);

        List<long> ids = rows.Select(r => r.Id).ToList();
        HashSet<long> paidImmediately = (await db.LedgerEntries.AsNoTracking()
                .Where(e => e.SourceType == SourceType && ids.Contains(e.SourceId) && e.AccountId != null)
                .Select(e => e.SourceId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        return rows.Select(o =>
        {
            (string name, string bucket) = categories.GetValueOrDefault(o.CategoryId, ("", ""));
            return new DirectExpenseDto(
                o.Id, o.ProjectId, o.CategoryId, name, bucket, o.PartyId, o.Date, o.Amount,
                paidImmediately.Contains(o.Id), o.Description, o.Status.ToString());
        }).ToList();
    }

    public async Task<bool> ReverseAsync(long id, string reason, CancellationToken cancellationToken)
    {
        Obligation? obligation = await db.Obligations
            .FirstOrDefaultAsync(o => o.Id == id && o.Type == ObligationType.DirectExpense, cancellationToken);
        if (obligation is null)
        {
            return false;
        }

        if (obligation.Status == ObligationStatus.Reversed)
        {
            throw Fail("id", "This expense is already reversed.");
        }

        await ledger.ReverseAsync(SourceType, id, reason, cancellationToken);
        obligation.Status = ObligationStatus.Reversed;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
