using ColourBricks.Application.Ledger;
using ColourBricks.Application.Payments;
using ColourBricks.Application.VendorPayments;
using ColourBricks.Domain.Allocations;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Services;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.VendorPayments;

public sealed class VendorPaymentService(
    AppDbContext db,
    ILedgerPostingService ledger,
    ILedgerQueryService ledgerQuery,
    IExpenseCategoryService categories,
    IPaymentModeService paymentModes) : IVendorPaymentService
{
    private const string SourceType = "VendorPayment";
    private const string PaymentDescription = "Vendor payment";
    private const string MultiProjectDescription = "Vendor payment (multi-project)";

    public async Task<VendorPaymentDto> PayAsync(
        RecordVendorPaymentRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Parties.AnyAsync(p => p.Id == request.VendorId, cancellationToken))
        {
            throw Fail("vendorId", "The vendor does not exist.");
        }

        if (!await db.Projects.AnyAsync(p => p.Id == request.ProjectId, cancellationToken))
        {
            throw Fail("projectId", "The project does not exist.");
        }

        long payableCategory = await categories.RequireIdAsync("vendor_payable", cancellationToken);
        decimal projectOutstanding = await ledgerQuery.GetPartyPayableBalanceForProjectAsync(
            request.VendorId, request.ProjectId, payableCategory, cancellationToken);

        // Anything over the project's outstanding is a vendor advance (BRD §25): it
        // settles the outstanding and the remainder is carried as a project-less credit.
        decimal settledPortion = Math.Min(request.Amount, Math.Max(0m, projectOutstanding));
        decimal advancePortion = Money.Round(request.Amount - settledPortion);

        await paymentModes.ValidateInstructionAsync(
            new PaymentInstruction(request.PaymentModeId, request.ReferenceNo, request.AccountId),
            cancellationToken);

        long? obligationId = request.ObligationIds is { Count: 1 } single ? single[0] : null;

        var settlement = new Settlement
        {
            Direction = SettlementDirection.Out,
            ProjectId = request.ProjectId,
            PartyId = request.VendorId,
            ObligationId = obligationId,
            Date = request.Date,
            Amount = request.Amount,
            PaymentModeId = request.PaymentModeId,
            AccountId = request.AccountId,
            ReferenceNo = request.ReferenceNo,
            Description = PaymentDescription,
            Status = SettlementStatus.Active,
        };
        db.Settlements.Add(settlement);
        await db.SaveChangesAsync(cancellationToken);

        if (advancePortion > 0m)
        {
            db.Allocations.Add(new Allocation
            {
                SettlementId = settlement.Id,
                ObligationId = null,
                ProjectId = null,
                PartyId = request.VendorId,
                Amount = advancePortion,
                Method = "Advance",
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        // Credit account (cash out), debit vendor payable. Never an expense category (BRD §22).
        // The settled portion is project-scoped; the advance portion is a project-less credit.
        var legs = new List<LedgerLeg>();
        if (settledPortion > 0m)
        {
            legs.Add(new LedgerLeg(payableCategory, Debit: settledPortion, Credit: 0m,
                ProjectId: request.ProjectId, PartyId: request.VendorId));
        }

        if (advancePortion > 0m)
        {
            legs.Add(new LedgerLeg(payableCategory, Debit: advancePortion, Credit: 0m, PartyId: request.VendorId));
        }

        if (request.AccountId is { } accountId)
        {
            legs.Add(new LedgerLeg(payableCategory, Debit: request.Amount, Credit: 0m, AccountId: accountId));
        }

        await ledger.PostAsync(
            new LedgerPosting(SourceType, settlement.Id, request.Date, legs), cancellationToken);

        decimal after = await ledgerQuery.GetPartyPayableBalanceForProjectAsync(
            request.VendorId, request.ProjectId, payableCategory, cancellationToken);

        return ToDto(settlement, after, advancePortion);
    }

    public async Task<IReadOnlyList<VendorPaymentDto>> ListForVendorAsync(
        long vendorId, long? projectId, CancellationToken cancellationToken)
    {
        IQueryable<Settlement> query = db.Settlements.AsNoTracking()
            .Where(s => s.PartyId == vendorId
                && s.Direction == SettlementDirection.Out
                && s.Description == PaymentDescription);

        if (projectId is { } p)
        {
            query = query.Where(s => s.ProjectId == p);
        }

        List<Settlement> rows = await query
            .OrderByDescending(s => s.Date).ThenByDescending(s => s.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(s => ToDto(s, 0m)).ToList();
    }

    public async Task<IReadOnlyList<VendorStatementRowDto>> StatementAsync(
        long vendorId, CancellationToken cancellationToken)
    {
        List<(DateOnly Date, long Id, string Kind, string Reference, decimal Purchase, decimal Paid)> events =
            (await db.Obligations.AsNoTracking()
                .Where(o => o.PartyId == vendorId && o.Type == ObligationType.VendorPurchase
                    && o.Status == ObligationStatus.Active)
                .Select(o => new { o.Date, o.Id, o.Reference, o.Amount })
                .ToListAsync(cancellationToken))
            .Select(o => (o.Date, o.Id, "Purchase", o.Reference ?? "Purchase", o.Amount, 0m))
            .ToList();

        events.AddRange((await db.Settlements.AsNoTracking()
                .Where(s => s.PartyId == vendorId && s.Direction == SettlementDirection.Out
                    && s.Status == SettlementStatus.Active)
                .Select(s => new { s.Date, s.Id, s.ReferenceNo, s.Amount })
                .ToListAsync(cancellationToken))
            .Select(s => (s.Date, s.Id, "Payment", s.ReferenceNo ?? "Payment", 0m, s.Amount)));

        decimal running = 0m;
        var rows = new List<VendorStatementRowDto>();
        foreach (var e in events
            .OrderBy(e => e.Date).ThenBy(e => e.Kind == "Purchase" ? 0 : 1).ThenBy(e => e.Id))
        {
            running += e.Purchase - e.Paid;
            rows.Add(new VendorStatementRowDto(e.Date, e.Kind, e.Reference, e.Purchase, e.Paid, running));
        }

        // The vendor's credit balance, if any, closes the statement (BRD §33, §25).
        long payableCategory = await categories.RequireIdAsync("vendor_payable", cancellationToken);
        decimal advance = await ledgerQuery.GetVendorAdvanceBalanceAsync(
            vendorId, payableCategory, cancellationToken);
        if (advance > 0m)
        {
            DateOnly asOf = rows.Count > 0 ? rows[^1].Date : DateOnly.FromDateTime(DateTime.UtcNow);
            rows.Add(new VendorStatementRowDto(asOf, "Advance", "Credit balance", 0m, advance, running));
        }

        return rows;
    }

    public async Task<ApplyVendorAdvanceDto> ApplyAdvanceAsync(
        ApplyVendorAdvanceRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0m)
        {
            throw Fail("amount", "The amount to apply must be greater than zero.");
        }

        Obligation? obligation = await db.Obligations
            .FirstOrDefaultAsync(o => o.Id == request.ObligationId
                && o.Type == ObligationType.VendorPurchase, cancellationToken);
        if (obligation is null)
        {
            throw Fail("obligationId", "The purchase does not exist.");
        }

        if (obligation.PartyId != request.VendorId)
        {
            throw Fail("obligationId", "That purchase belongs to a different vendor.");
        }

        if (obligation.Status != ObligationStatus.Active)
        {
            throw Fail("obligationId", "That purchase has been reversed.");
        }

        long payableCategory = await categories.RequireIdAsync("vendor_payable", cancellationToken);

        decimal available = await ledgerQuery.GetVendorAdvanceBalanceAsync(
            request.VendorId, payableCategory, cancellationToken);
        if (Money.Round(request.Amount) > Money.Round(available) + Money.Epsilon)
        {
            throw Fail("amount", $"Only {available:0.00} of advance is available for this vendor.");
        }

        decimal purchaseOutstanding = await ledgerQuery.GetPartyPayableBalanceForProjectAsync(
            request.VendorId, obligation.ProjectId, payableCategory, cancellationToken);
        if (Money.Round(request.Amount) > Money.Round(purchaseOutstanding) + Money.Epsilon)
        {
            throw Fail("amount", $"Only {purchaseOutstanding:0.00} is outstanding on that purchase.");
        }

        decimal applied = Money.Round(request.Amount);

        // Two non-cash allocation rows: one settles the purchase, one consumes the advance.
        var settleRow = new Allocation
        {
            SettlementId = null,
            ObligationId = obligation.Id,
            ProjectId = obligation.ProjectId,
            PartyId = request.VendorId,
            Amount = applied,
            Method = "AdvanceApplied",
        };
        db.Allocations.Add(settleRow);
        db.Allocations.Add(new Allocation
        {
            SettlementId = null,
            ObligationId = null,
            ProjectId = null,
            PartyId = request.VendorId,
            Amount = -applied,
            Method = "AdvanceApplied",
        });
        await db.SaveChangesAsync(cancellationToken);

        // Ledger: move the credit from the project-less advance onto the purchase's project.
        // No account leg — an advance application moves no cash (BRD §25).
        var legs = new List<LedgerLeg>
        {
            new(payableCategory, Debit: applied, Credit: 0m,
                ProjectId: obligation.ProjectId, PartyId: request.VendorId),
            new(payableCategory, Debit: 0m, Credit: applied, PartyId: request.VendorId),
        };
        await ledger.PostAsync(
            new LedgerPosting("VendorAdvanceApplied", settleRow.Id, request.Date, legs), cancellationToken);

        decimal advanceAfter = await ledgerQuery.GetVendorAdvanceBalanceAsync(
            request.VendorId, payableCategory, cancellationToken);
        decimal purchaseAfter = await ledgerQuery.GetPartyPayableBalanceForProjectAsync(
            request.VendorId, obligation.ProjectId, payableCategory, cancellationToken);

        return new ApplyVendorAdvanceDto(obligation.Id, applied, advanceAfter, purchaseAfter);
    }

    public async Task<bool> ReverseAsync(long paymentId, string reason, CancellationToken cancellationToken)
    {
        Settlement? settlement = await db.Settlements
            .FirstOrDefaultAsync(s => s.Id == paymentId
                && s.Direction == SettlementDirection.Out
                && (s.Description == PaymentDescription || s.Description == MultiProjectDescription),
                cancellationToken);
        if (settlement is null)
        {
            return false;
        }

        if (settlement.Status == SettlementStatus.Reversed)
        {
            throw Fail("id", "This payment is already reversed.");
        }

        await ledger.ReverseAsync(SourceType, paymentId, reason, cancellationToken);
        settlement.Status = SettlementStatus.Reversed;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static VendorPaymentDto ToDto(Settlement s, decimal outstandingAfter, decimal advanceCreated = 0m) => new(
        s.Id, s.PartyId!.Value, s.ProjectId ?? 0, s.Date, s.Amount, s.PaymentModeId,
        s.AccountId, s.ReferenceNo, s.Status.ToString(), outstandingAfter, advanceCreated);

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
