using System.Text.Json;
using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Allocations;
using ColourBricks.Application.Ledger;
using ColourBricks.Application.Payments;
using ColourBricks.Domain.Services;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Allocations;

public sealed class MultiProjectVendorPaymentService(
    AppDbContext db,
    IAllocationEngine engine,
    ILedgerPostingService ledger,
    IExpenseCategoryService categories,
    IAuditService audit,
    IPaymentModeService paymentModes) : IMultiProjectVendorPaymentService
{
    private const string SourceType = "VendorPayment";

    public Task<AllocationProposalDto> ProposeAsync(
        long vendorId, decimal amount, CancellationToken cancellationToken) =>
        engine.ProposeAsync(vendorId, amount, "Fifo", cancellationToken);

    public async Task<MultiProjectPaymentDto> PayAsync(
        RecordMultiProjectPaymentRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Parties.AnyAsync(p => p.Id == request.VendorId, cancellationToken))
        {
            throw Fail("vendorId", "The vendor does not exist.");
        }

        await paymentModes.ValidateInstructionAsync(
            new PaymentInstruction(request.PaymentModeId, request.ReferenceNo, request.AccountId),
            cancellationToken);

        // An explicit AdvanceAmount (client request, 2026-09-04) also counts as a manual
        // override: it must never trigger a FIFO proposal against the vendor's real open
        // obligations for the advance portion of the amount.
        bool manual = request.Allocations is { Count: > 0 } || request.AdvanceAmount > 0m;
        string method = manual ? "Manual" : "Fifo";

        if (manual && string.IsNullOrWhiteSpace(request.OverrideReason))
        {
            throw Fail("overrideReason", "A reason is required when overriding the FIFO allocation.");
        }

        // Always compute the FIFO proposal (for audit — "what FIFO would have done" vs
        // "what was actually applied") even when a manual override/advance is in play.
        AllocationProposalDto proposal = await engine.ProposeAsync(
            request.VendorId, request.Amount, "Fifo", cancellationToken);

        IReadOnlyList<AllocationInputDto> allocations = manual
            ? request.Allocations ?? Array.Empty<AllocationInputDto>()
            : proposal.Lines
                .Select(l => new AllocationInputDto(l.ProjectId, l.ObligationId, l.Allocated))
                .ToList();

        decimal explicitAdvance = Money.Round(request.AdvanceAmount);
        decimal totalAllocated = Money.Round(allocations.Sum(a => a.Amount));
        decimal combined = Money.Round(totalAllocated + explicitAdvance);
        decimal advance = explicitAdvance;

        if (combined > Money.Round(request.Amount) + Money.Epsilon)
        {
            throw Fail("amount",
                $"Allocations total {combined:0.00}, more than the {request.Amount:0.00} payment.");
        }

        if (combined < Money.Round(request.Amount))
        {
            // A manual override (or an explicit project-less advance) must account for
            // the whole payment; only pure FIFO leaves a genuine advance when the
            // vendor's outstanding is less than the payment.
            if (manual || explicitAdvance > 0m)
            {
                throw Fail("amount",
                    $"Allocations total {combined:0.00}; they must equal the {request.Amount:0.00} payment.");
            }

            advance = Money.Round(request.Amount - totalAllocated);
        }

        var settlement = new Settlement
        {
            Direction = SettlementDirection.Out,
            ProjectId = null,
            PartyId = request.VendorId,
            Date = request.Date,
            Amount = request.Amount,
            PaymentModeId = request.PaymentModeId,
            AccountId = request.AccountId,
            ReferenceNo = request.ReferenceNo,
            Description = "Vendor payment (multi-project)",
            Status = SettlementStatus.Active,
        };
        db.Settlements.Add(settlement);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await engine.ApplyAsync(settlement.Id, allocations, method, cancellationToken);
        }
        catch
        {
            // The allocation failed (e.g. a concurrent payment took the outstanding).
            // Roll the orphan settlement back so nothing is left half-recorded.
            db.Settlements.Remove(settlement);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        if (advance > 0m)
        {
            // The unallocatable remainder is a vendor advance (BRD §25): an allocation
            // with no obligation and no project, mirrored by a project-less payable debit.
            db.Allocations.Add(new Domain.Allocations.Allocation
            {
                SettlementId = settlement.Id,
                ObligationId = null,
                ProjectId = null,
                PartyId = request.VendorId,
                Amount = advance,
                Method = "Advance",
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        long payableCategory = await categories.RequireIdAsync("vendor_payable", cancellationToken);
        var legs = allocations
            .Select(a => new LedgerLeg(payableCategory, Debit: Money.Round(a.Amount), Credit: 0m,
                ProjectId: a.ProjectId, PartyId: request.VendorId))
            .ToList();

        if (advance > 0m)
        {
            legs.Add(new LedgerLeg(payableCategory, Debit: advance, Credit: 0m, PartyId: request.VendorId));
        }

        if (request.AccountId is { } accountId)
        {
            legs.Add(new LedgerLeg(payableCategory, Debit: Money.Round(request.Amount), Credit: 0m, AccountId: accountId));
        }

        if (manual)
        {
            // Record what FIFO proposed and what was actually applied (BRD §24, rule 24).
            audit.RecordAction("vendor_payment_allocation", "allocation_override", settlement.Id.ToString(),
                JsonSerializer.Serialize(new
                {
                    reason = request.OverrideReason!.Trim(),
                    proposed = proposal.Lines.Select(l => new { l.ProjectId, l.Allocated }).ToArray(),
                    applied = allocations.Select(a => new { a.ProjectId, a.ObligationId, a.Amount }).ToArray(),
                }));
        }

        await ledger.PostAsync(
            new LedgerPosting(SourceType, settlement.Id, request.Date, legs), cancellationToken);

        List<AllocationLineDto> appliedLines = manual
            ? allocations.Select(a => new AllocationLineDto(
                a.ProjectId, "", a.ObligationId, null, 0m, Money.Round(a.Amount), 0m)).ToList()
            : [.. proposal.Lines];

        return new MultiProjectPaymentDto(
            settlement.Id, request.VendorId, request.Amount, method, appliedLines, advance);
    }

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
