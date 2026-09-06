using ColourBricks.Application.CommonExpenses;
using ColourBricks.Application.Ledger;
using ColourBricks.Application.Payments;
using ColourBricks.Domain.CommonExpenses;
using ColourBricks.Domain.Services;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.CommonExpenses;

/// <summary>P6-T01 — company-level Personal / Office / Savings expense entry (BRD §44).</summary>
public sealed class CommonExpenseService(
    AppDbContext db,
    ILedgerPostingService ledger,
    IExpenseCategoryService categories,
    IPaymentModeService paymentModes) : ICommonExpenseService
{
    private const string SourceType = "CommonExpense";

    private static readonly Dictionary<CommonExpenseType, string> CategorySlug = new()
    {
        [CommonExpenseType.Personal] = "personal_common",
        [CommonExpenseType.Office] = "office_common",
        [CommonExpenseType.Savings] = "savings_allocation",
        [CommonExpenseType.Custom] = "custom_common",
    };

    public async Task<CommonExpenseDto> RecordAsync(RecordCommonExpenseRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse(request.Type, ignoreCase: true, out CommonExpenseType type))
        {
            throw Fail("type", "Type must be Personal, Office or Savings.");
        }

        if (string.IsNullOrWhiteSpace(request.SubCategory))
        {
            throw Fail("subCategory", "A sub-category is required (BRD §44).");
        }

        if (request.Amount <= 0m)
        {
            throw Fail("amount", "The amount must be greater than zero.");
        }

        await paymentModes.ValidateInstructionAsync(
            new PaymentInstruction(request.PaymentModeId, request.ReferenceNo, request.AccountId), ct);

        var expense = new CommonExpense
        {
            Type = type,
            SubCategory = request.SubCategory.Trim(),
            Date = request.Date,
            Amount = Money.Round(request.Amount),
            PaymentModeId = request.PaymentModeId,
            AccountId = request.AccountId,
            ReferenceNo = request.ReferenceNo,
            Description = request.Description,
            Status = CommonExpenseStatus.Active,
        };
        db.Set<CommonExpense>().Add(expense);

        // A reconciliation anchor only, no ledger legs of its own (client request,
        // 2026-09-06) — lets this expense optionally be linked to a bank transaction
        // via the existing reconcile-debit flow. The real posting below is unchanged.
        Settlement? anchor = null;
        if (request.AccountId is { } anchorAccountId)
        {
            anchor = new Settlement
            {
                Direction = SettlementDirection.Out,
                Date = expense.Date,
                Amount = expense.Amount,
                PaymentModeId = expense.PaymentModeId,
                AccountId = anchorAccountId,
                ReferenceNo = expense.ReferenceNo,
                Description = $"{type} expense (reconciliation anchor)",
                Status = SettlementStatus.Active,
            };
            db.Settlements.Add(anchor);
        }

        await db.SaveChangesAsync(ct);

        long category = await categories.RequireIdAsync(CategorySlug[type], ct);
        var legs = new List<LedgerLeg>
        {
            // Company-level: no project until an allocation run distributes it (P6-T02).
            new(category, Debit: expense.Amount, Credit: 0m),
        };
        if (request.AccountId is { } accountId)
        {
            // Cash out of the account (plan.md §5.3 — balance = opening + credits − debits).
            legs.Add(new LedgerLeg(category, Debit: expense.Amount, Credit: 0m, AccountId: accountId));
        }

        await ledger.PostAsync(new LedgerPosting(SourceType, expense.Id, request.Date, legs), ct);

        return ToDto(expense, anchor?.Id);
    }

    public async Task<IReadOnlyList<CommonExpenseDto>> ListAsync(CommonExpenseQuery q, CancellationToken ct)
    {
        IQueryable<CommonExpense> query = db.Set<CommonExpense>().AsNoTracking()
            .Where(e => e.Status == CommonExpenseStatus.Active);

        if (Enum.TryParse(q.Type, ignoreCase: true, out CommonExpenseType t))
        {
            query = query.Where(e => e.Type == t);
        }

        if (!string.IsNullOrWhiteSpace(q.SubCategory))
        {
            query = query.Where(e => e.SubCategory == q.SubCategory);
        }

        if (q.DateFrom is { } from) query = query.Where(e => e.Date >= from);
        if (q.DateTo is { } to) query = query.Where(e => e.Date <= to);
        if (q.PaymentModeId is { } pm) query = query.Where(e => e.PaymentModeId == pm);
        if (q.AccountId is { } acc) query = query.Where(e => e.AccountId == acc);

        List<CommonExpense> rows = await query
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)
            .ToListAsync(ct);
        return rows.Select(e => ToDto(e)).ToList();
    }

    public async Task<CommonExpenseSummaryDto> SummaryAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        // Ledger-derived, not a sum of the CommonExpense table: a field officer's
        // no-project bill (client request, 2026-09-04) posts to these same four
        // company-level categories but lives in a different table, and an allocation
        // run's project-level debit is offset here by its matching company-level
        // credit — so this stays correct ("still at company level") either way.
        Dictionary<CommonExpenseType, long> categoryIds = new();
        foreach ((CommonExpenseType type, string slug) in CategorySlug)
        {
            categoryIds[type] = await categories.RequireIdAsync(slug, ct);
        }

        // RecordAsync posts a second leg under the SAME category when an account is
        // given (to move cash out of that account) — exclude it here or the amount
        // double-counts; AccountId == null isolates the one true expense-recognition leg.
        IQueryable<Domain.Ledger.LedgerEntry> query = db.LedgerEntries.AsNoTracking()
            .Where(e => e.ProjectId == null && e.AccountId == null && categoryIds.Values.Contains(e.CategoryId));
        if (from is { } f) query = query.Where(e => e.EntryDate >= f);
        if (to is { } t) query = query.Where(e => e.EntryDate <= t);

        Dictionary<long, decimal> byCategory = await query
            .GroupBy(e => e.CategoryId)
            .Select(g => new { CategoryId = g.Key, Net = g.Sum(x => x.Debit - x.Credit) })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Net, ct);

        decimal Net(CommonExpenseType t) => Money.Round(byCategory.GetValueOrDefault(categoryIds[t], 0m));
        decimal personal = Net(CommonExpenseType.Personal);
        decimal office = Net(CommonExpenseType.Office);
        decimal savings = Net(CommonExpenseType.Savings);
        decimal custom = Net(CommonExpenseType.Custom);
        return new CommonExpenseSummaryDto(personal, office, savings, personal + office + custom, custom);
    }

    public async Task<bool> ReverseAsync(long id, string reason, CancellationToken ct)
    {
        CommonExpense? expense = await db.Set<CommonExpense>()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (expense is null)
        {
            return false;
        }

        if (expense.Status == CommonExpenseStatus.Reversed)
        {
            throw new ValidationException([new ValidationFailure("id", "This expense is already reversed.")]);
        }

        await ledger.ReverseAsync(SourceType, id, reason, ct);
        expense.Status = CommonExpenseStatus.Reversed;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static CommonExpenseDto ToDto(CommonExpense e, long? settlementId = null) => new(
        e.Id, e.Type.ToString(), e.SubCategory, e.Date, e.Amount, e.PaymentModeId, e.AccountId,
        e.ReferenceNo, e.Description, e.Status.ToString(), settlementId);

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
