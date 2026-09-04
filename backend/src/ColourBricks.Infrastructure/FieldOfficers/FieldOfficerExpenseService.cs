using ColourBricks.Application.FieldOfficers;
using ColourBricks.Application.Ledger;
using ColourBricks.Domain.CommonExpenses;
using ColourBricks.Domain.FieldOfficers;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.FieldOfficers;

/// <summary>
/// P10-T03 — a field officer's no-project bill (client request, 2026-09-04). Posts a
/// payable to him instead of paying instantly, so it nets against whatever advance
/// he already holds and shows up in his outstanding — reusing the vendor payable/
/// advance machinery, not a parallel one.
/// </summary>
public sealed class FieldOfficerExpenseService(
    AppDbContext db,
    ILedgerPostingService ledger,
    IExpenseCategoryService categories) : IFieldOfficerExpenseService
{
    private const string SourceType = "FieldOfficerExpense";

    private static readonly Dictionary<CommonExpenseType, string> CategorySlug = new()
    {
        [CommonExpenseType.Personal] = "personal_common",
        [CommonExpenseType.Office] = "office_common",
        [CommonExpenseType.Savings] = "savings_allocation",
        [CommonExpenseType.Custom] = "custom_common",
    };

    public async Task<FieldOfficerExpenseDto> RecordAsync(RecordFieldOfficerExpenseRequest request, CancellationToken ct)
    {
        Party officer = await db.Parties
            .FirstOrDefaultAsync(p => p.Id == request.FieldOfficerId, ct)
            ?? throw Fail("fieldOfficerId", "The field officer does not exist.");
        if (!officer.Types.HasFlag(PartyType.FieldOfficer))
        {
            throw Fail("fieldOfficerId", "This party is not marked as a field officer.");
        }

        if (!Enum.TryParse(request.Type, ignoreCase: true, out CommonExpenseType type))
        {
            throw Fail("type", "Type must be Personal, Office, Savings or Custom.");
        }

        if (request.Amount <= 0m)
        {
            throw Fail("amount", "The amount must be greater than zero.");
        }

        var expense = new FieldOfficerExpense
        {
            FieldOfficerId = request.FieldOfficerId,
            Type = type,
            Date = request.Date,
            Amount = Money.Round(request.Amount),
            ReferenceNo = request.ReferenceNo,
            Description = request.Description,
            Status = FieldOfficerExpenseStatus.Active,
        };
        db.Set<FieldOfficerExpense>().Add(expense);
        await db.SaveChangesAsync(ct);

        long expenseCategory = await categories.RequireIdAsync(CategorySlug[type], ct);
        long payableCategory = await categories.RequireIdAsync("vendor_payable", ct);

        await ledger.PostAsync(new LedgerPosting(SourceType, expense.Id, request.Date,
        [
            // Company-level: no project, exactly like a Personal/Office/Savings/Custom
            // common expense — but credited to the officer's payable, not cash.
            new LedgerLeg(expenseCategory, Debit: expense.Amount, Credit: 0m),
            new LedgerLeg(payableCategory, Debit: 0m, Credit: expense.Amount, PartyId: request.FieldOfficerId),
        ]), ct);

        return ToDto(expense);
    }

    public async Task<IReadOnlyList<FieldOfficerExpenseDto>> ListAsync(FieldOfficerExpenseQuery q, CancellationToken ct)
    {
        IQueryable<FieldOfficerExpense> query = db.Set<FieldOfficerExpense>().AsNoTracking();

        if (q.FieldOfficerId is { } id) query = query.Where(e => e.FieldOfficerId == id);
        if (Enum.TryParse(q.Type, ignoreCase: true, out CommonExpenseType t)) query = query.Where(e => e.Type == t);
        if (q.DateFrom is { } from) query = query.Where(e => e.Date >= from);
        if (q.DateTo is { } to) query = query.Where(e => e.Date <= to);

        List<FieldOfficerExpense> rows = await query
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<bool> ReverseAsync(long id, string reason, CancellationToken ct)
    {
        FieldOfficerExpense? expense = await db.Set<FieldOfficerExpense>()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (expense is null)
        {
            return false;
        }

        if (expense.Status == FieldOfficerExpenseStatus.Reversed)
        {
            throw new ValidationException([new ValidationFailure("id", "This bill is already reversed.")]);
        }

        await ledger.ReverseAsync(SourceType, id, reason, ct);
        expense.Status = FieldOfficerExpenseStatus.Reversed;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static FieldOfficerExpenseDto ToDto(FieldOfficerExpense e) => new(
        e.Id, e.FieldOfficerId, e.Type.ToString(), e.Date, e.Amount, e.ReferenceNo, e.Description, e.Status.ToString());

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
