using ColourBricks.Application.CustomWork;
using ColourBricks.Application.Ledger;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.CustomWork;

public sealed class CustomWorkService(
    AppDbContext db,
    ILedgerPostingService ledger,
    IExpenseCategoryService categories) : ICustomWorkService
{
    private const string SourceType = "CustomWork";
    private const string PaymentSource = "CustomWorkPayment";

    public async Task<CustomWorkDto> RecordAsync(
        RecordCustomWorkRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == request.ProjectId, cancellationToken))
        {
            throw Fail("projectId", "The project does not exist.");
        }

        if (request.PartyId is { } partyId
            && !await db.Parties.AnyAsync(p => p.Id == partyId, cancellationToken))
        {
            throw Fail("partyId", "The selected vendor/subcontractor does not exist.");
        }

        long costCategory = await categories.RequireIdAsync("customized_work", cancellationToken);
        long payableCategory = await categories.RequireIdAsync("custom_work_payable", cancellationToken);

        var obligation = new Obligation
        {
            Type = ObligationType.CustomWork,
            ProjectId = request.ProjectId,
            PartyId = request.PartyId,
            DepartmentId = request.DepartmentId,
            Date = request.Date,
            Amount = request.ActualCost,            // actual cost drives the ledger
            EstimatedAmount = request.EstimatedCost, // informational only
            Reference = request.WorkType,
            Description = request.Description,
            CategoryId = costCategory,
            Status = ObligationStatus.Active,
        };
        db.Obligations.Add(obligation);
        await db.SaveChangesAsync(cancellationToken);

        // Only the actual cost posts.
        await ledger.PostAsync(new LedgerPosting(SourceType, obligation.Id, request.Date,
        [
            new LedgerLeg(costCategory, Debit: request.ActualCost, Credit: 0m,
                ProjectId: request.ProjectId, PartyId: request.PartyId),
            new LedgerLeg(payableCategory, Debit: 0m, Credit: request.ActualCost,
                ProjectId: request.ProjectId, PartyId: request.PartyId),
        ]), cancellationToken);

        return (await ListAsync(request.ProjectId, cancellationToken)).First(x => x.Id == obligation.Id);
    }

    public async Task<IReadOnlyList<CustomWorkDto>> ListAsync(
        long projectId, CancellationToken cancellationToken)
    {
        List<Obligation> rows = await db.Obligations.AsNoTracking()
            .Where(o => o.Type == ObligationType.CustomWork && o.ProjectId == projectId)
            .OrderByDescending(o => o.Date).ThenByDescending(o => o.Id)
            .ToListAsync(cancellationToken);

        List<long> partyIds = rows.Where(o => o.PartyId != null).Select(o => o.PartyId!.Value).ToList();
        Dictionary<long, string> names = await db.Parties.AsNoTracking()
            .Where(p => partyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return rows.Select(o =>
        {
            decimal estimated = o.EstimatedAmount ?? 0m;
            return new CustomWorkDto(
                o.Id, o.ProjectId, o.PartyId,
                o.PartyId is { } p ? names.GetValueOrDefault(p) : null,
                o.DepartmentId, o.Date, o.Reference, o.Description,
                estimated, o.Amount, o.Amount - estimated, o.Status.ToString());
        }).ToList();
    }

    public async Task<bool> ReverseAsync(long id, string reason, CancellationToken cancellationToken)
    {
        Obligation? obligation = await db.Obligations
            .FirstOrDefaultAsync(o => o.Id == id && o.Type == ObligationType.CustomWork, cancellationToken);
        if (obligation is null)
        {
            return false;
        }

        if (obligation.Status == ObligationStatus.Reversed)
        {
            throw Fail("id", "This custom work record is already reversed.");
        }

        await ledger.ReverseAsync(SourceType, id, reason, cancellationToken);

        // Mirrors VendorPurchaseService.ReverseAsync: reversing the record also reverses
        // any active payment made against it, or the books would show cash paid out
        // against a payable that no longer exists.
        List<Settlement> payments = await db.Settlements
            .Where(s => s.ObligationId == id
                && s.Direction == SettlementDirection.Out
                && s.Status == SettlementStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (Settlement payment in payments)
        {
            await ledger.ReverseAsync(PaymentSource, payment.Id, reason, cancellationToken);
            payment.Status = SettlementStatus.Reversed;
        }

        obligation.Status = ObligationStatus.Reversed;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
