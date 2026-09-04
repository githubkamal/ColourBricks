using ColourBricks.Application.CustomWork;
using ColourBricks.Application.Ledger;
using ColourBricks.Application.Payments;
using ColourBricks.Domain.Services;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.CustomWork;

/// <summary>
/// Settles a custom-work obligation (client request, 2026-09-04) — the same shape as
/// <c>VendorPaymentService.PayAsync</c> but for the "custom_work_payable" category, one
/// project + one party at a time, no advance overflow (paying more than is outstanding
/// is rejected, not parked as a credit — nothing asked for that here).
/// </summary>
public sealed class CustomWorkPaymentService(
    AppDbContext db,
    ILedgerPostingService ledger,
    ILedgerQueryService ledgerQuery,
    IExpenseCategoryService categories,
    IPaymentModeService paymentModes) : ICustomWorkPaymentService
{
    private const string SourceType = "CustomWorkPayment";
    internal const string PaymentDescription = "Custom work payment";

    public async Task<CustomWorkPaymentDto> PayAsync(
        RecordCustomWorkPaymentRequest request, CancellationToken ct)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == request.ProjectId, ct))
        {
            throw Fail("projectId", "The project does not exist.");
        }

        if (!await db.Parties.AnyAsync(p => p.Id == request.PartyId, ct))
        {
            throw Fail("partyId", "The party does not exist.");
        }

        if (request.Amount <= 0m)
        {
            throw Fail("amount", "The amount must be greater than zero.");
        }

        long payableCategory = await categories.RequireIdAsync("custom_work_payable", ct);
        decimal outstanding = await ledgerQuery.GetPartyPayableBalanceForProjectAsync(
            request.PartyId, request.ProjectId, payableCategory, ct);
        if (Money.Round(request.Amount) > Money.Round(outstanding) + Money.Epsilon)
        {
            throw Fail("amount", $"Only {outstanding:0.00} is outstanding for this custom work.");
        }

        if (request.CustomWorkId is { } customWorkId
            && !await db.Obligations.AnyAsync(o => o.Id == customWorkId
                && o.Type == Domain.Obligations.ObligationType.CustomWork
                && o.ProjectId == request.ProjectId && o.PartyId == request.PartyId, ct))
        {
            throw Fail("customWorkId", "That custom work record does not belong to this project/party.");
        }

        await paymentModes.ValidateInstructionAsync(
            new PaymentInstruction(request.PaymentModeId, request.ReferenceNo, request.AccountId), ct);

        var settlement = new Settlement
        {
            Direction = SettlementDirection.Out,
            ProjectId = request.ProjectId,
            PartyId = request.PartyId,
            ObligationId = request.CustomWorkId,
            Date = request.Date,
            Amount = request.Amount,
            PaymentModeId = request.PaymentModeId,
            AccountId = request.AccountId,
            ReferenceNo = request.ReferenceNo,
            Description = PaymentDescription,
            Status = SettlementStatus.Active,
        };
        db.Settlements.Add(settlement);
        await db.SaveChangesAsync(ct);

        var legs = new List<LedgerLeg>
        {
            new(payableCategory, Debit: Money.Round(request.Amount), Credit: 0m,
                ProjectId: request.ProjectId, PartyId: request.PartyId),
        };
        if (request.AccountId is { } accountId)
        {
            legs.Add(new LedgerLeg(payableCategory, Debit: Money.Round(request.Amount), Credit: 0m, AccountId: accountId));
        }

        await ledger.PostAsync(new LedgerPosting(SourceType, settlement.Id, request.Date, legs), ct);

        decimal after = await ledgerQuery.GetPartyPayableBalanceForProjectAsync(
            request.PartyId, request.ProjectId, payableCategory, ct);

        return ToDto(settlement, after);
    }

    public async Task<IReadOnlyList<CustomWorkPaymentDto>> ListAsync(
        long projectId, long partyId, CancellationToken ct)
    {
        List<Settlement> rows = await db.Settlements.AsNoTracking()
            .Where(s => s.ProjectId == projectId && s.PartyId == partyId
                && s.Direction == SettlementDirection.Out && s.Description == PaymentDescription)
            .OrderByDescending(s => s.Date).ThenByDescending(s => s.Id)
            .ToListAsync(ct);
        return rows.Select(s => ToDto(s, 0m)).ToList();
    }

    public async Task<bool> ReverseAsync(long paymentId, string reason, CancellationToken ct)
    {
        Settlement? settlement = await db.Settlements
            .FirstOrDefaultAsync(s => s.Id == paymentId
                && s.Direction == SettlementDirection.Out
                && s.Description == PaymentDescription, ct);
        if (settlement is null)
        {
            return false;
        }

        if (settlement.Status == SettlementStatus.Reversed)
        {
            throw Fail("id", "This payment is already reversed.");
        }

        await ledger.ReverseAsync(SourceType, paymentId, reason, ct);
        settlement.Status = SettlementStatus.Reversed;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static CustomWorkPaymentDto ToDto(Settlement s, decimal outstandingAfter) => new(
        s.Id, s.ProjectId!.Value, s.PartyId!.Value, s.Date, s.Amount, s.PaymentModeId,
        s.AccountId, s.ReferenceNo, s.Status.ToString(), outstandingAfter);

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
