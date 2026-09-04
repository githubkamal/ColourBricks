using ColourBricks.Application.Ledger;
using ColourBricks.Application.Payments;
using ColourBricks.Application.VendorPurchases;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Services;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.VendorPurchases;

public sealed class VendorPurchaseService(
    AppDbContext db,
    ILedgerPostingService ledger,
    ILedgerQueryService ledgerQuery,
    IExpenseCategoryService categories,
    IPaymentModeService paymentModes) : IVendorPurchaseService
{
    private const string PurchaseSource = "VendorPurchase";
    private const string PaymentSource = "VendorPurchasePayment";

    public async Task<RecordVendorPurchaseResult> RecordAsync(
        RecordVendorPurchaseRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == request.ProjectId, cancellationToken))
        {
            throw Fail("projectId", "The project does not exist.");
        }

        if (!await db.Parties.AnyAsync(p => p.Id == request.VendorId, cancellationToken))
        {
            throw Fail("vendorId", "The vendor does not exist.");
        }

        List<(PurchaseLineInput Input, decimal LineTotal)> lines = request.Lines
            .Select(l => (l, Money.Round(l.Quantity * l.Rate + l.TaxAmount)))
            .ToList();

        decimal linesTotal = Money.Round(lines.Sum(l => l.LineTotal));
        if (linesTotal != Money.Round(request.Total))
        {
            throw Fail("total",
                $"Line totals ({linesTotal:0.00}) do not sum to the header total ({request.Total:0.00}).");
        }

        decimal partPayment = request.PartPayment ?? 0m;
        if (partPayment > linesTotal)
        {
            throw Fail("partPayment", "The part-payment cannot exceed the purchase total.");
        }

        bool duplicateInvoice = !string.IsNullOrWhiteSpace(request.InvoiceNumber)
            && await db.Obligations.AnyAsync(o =>
                o.Type == ObligationType.VendorPurchase
                && o.PartyId == request.VendorId
                && o.Reference == request.InvoiceNumber
                && o.Status == ObligationStatus.Active, cancellationToken);

        long expenseCategory = await categories.RequireIdAsync("materials", cancellationToken);
        long payableCategory = await categories.RequireIdAsync("vendor_payable", cancellationToken);

        var obligation = new Obligation
        {
            Type = ObligationType.VendorPurchase,
            ProjectId = request.ProjectId,
            PartyId = request.VendorId,
            Date = request.Date,
            Amount = linesTotal,
            Reference = request.InvoiceNumber,
            Description = request.Description,
            CategoryId = expenseCategory,
            Status = ObligationStatus.Active,
        };
        foreach ((PurchaseLineInput input, decimal lineTotal) in lines)
        {
            obligation.Lines.Add(new ObligationLine
            {
                ItemId = input.ItemId,
                ItemName = input.ItemName.Trim(),
                Quantity = input.Quantity,
                Unit = input.Unit.Trim(),
                Rate = input.Rate,
                TaxAmount = input.TaxAmount,
                LineTotal = lineTotal,
            });
        }
        db.Obligations.Add(obligation);
        await db.SaveChangesAsync(cancellationToken);

        // Obligation posting: debit project expense, credit vendor payable. No cash.
        await ledger.PostAsync(new LedgerPosting(PurchaseSource, obligation.Id, request.Date,
        [
            new LedgerLeg(expenseCategory, Debit: linesTotal, Credit: 0m, ProjectId: request.ProjectId),
            new LedgerLeg(payableCategory, Debit: 0m, Credit: linesTotal,
                ProjectId: request.ProjectId, PartyId: request.VendorId),
        ]), cancellationToken);

        if (partPayment > 0m)
        {
            await paymentModes.ValidateInstructionAsync(
                new PaymentInstruction(
                    request.PartPaymentModeId!.Value, request.PartPaymentReference, request.PartPaymentAccountId),
                cancellationToken);

            var settlement = new Settlement
            {
                Direction = SettlementDirection.Out,
                ProjectId = request.ProjectId,
                PartyId = request.VendorId,
                ObligationId = obligation.Id,
                Date = request.Date,
                Amount = partPayment,
                PaymentModeId = request.PartPaymentModeId!.Value,
                AccountId = request.PartPaymentAccountId,
                ReferenceNo = request.PartPaymentReference,
                Description = "Inline part-payment",
                Status = SettlementStatus.Active,
            };
            db.Settlements.Add(settlement);
            await db.SaveChangesAsync(cancellationToken);

            var legs = new List<LedgerLeg>
            {
                new(payableCategory, Debit: partPayment, Credit: 0m,
                    ProjectId: request.ProjectId, PartyId: request.VendorId),
            };
            if (request.PartPaymentAccountId is { } accountId)
            {
                legs.Add(new LedgerLeg(payableCategory, Debit: partPayment, Credit: 0m, AccountId: accountId));
            }

            await ledger.PostAsync(
                new LedgerPosting(PaymentSource, settlement.Id, request.Date, legs), cancellationToken);
        }

        VendorPurchaseDto dto = (await GetAsync(obligation.Id, cancellationToken))!;
        return new RecordVendorPurchaseResult(dto, duplicateInvoice);
    }

    public async Task<VendorPurchaseDto?> GetAsync(long id, CancellationToken cancellationToken)
    {
        Obligation? obligation = await db.Obligations.AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id && o.Type == ObligationType.VendorPurchase, cancellationToken);
        if (obligation is null)
        {
            return null;
        }

        return await ToDtoAsync(obligation, cancellationToken);
    }

    public async Task<IReadOnlyList<VendorPurchaseDto>> ListAsync(
        long? projectId, long? vendorId, CancellationToken cancellationToken)
    {
        IQueryable<Obligation> query = db.Obligations.AsNoTracking()
            .Include(o => o.Lines)
            .Where(o => o.Type == ObligationType.VendorPurchase);

        if (projectId is { } p)
        {
            query = query.Where(o => o.ProjectId == p);
        }

        if (vendorId is { } v)
        {
            query = query.Where(o => o.PartyId == v);
        }

        List<Obligation> rows = await query
            .OrderByDescending(o => o.Date).ThenByDescending(o => o.Id)
            .ToListAsync(cancellationToken);

        var dtos = new List<VendorPurchaseDto>(rows.Count);
        foreach (Obligation row in rows)
        {
            dtos.Add(await ToDtoAsync(row, cancellationToken));
        }

        return dtos;
    }

    public async Task<decimal> VendorOutstandingAsync(long vendorId, CancellationToken cancellationToken)
    {
        long payableCategory = await categories.RequireIdAsync("vendor_payable", cancellationToken);
        decimal net = await ledgerQuery.GetPartyPayableBalanceAsync(vendorId, payableCategory, cancellationToken);
        decimal advance = await ledgerQuery.GetVendorAdvanceBalanceAsync(vendorId, payableCategory, cancellationToken);

        // net = (Σ per-project balances) − advance; add the advance back so this reports
        // only what is still owed. The advance is surfaced separately (BRD §25, P3-T05).
        return net + advance;
    }

    public async Task<bool> ReverseAsync(long id, string reason, CancellationToken cancellationToken)
    {
        Obligation? obligation = await db.Obligations
            .FirstOrDefaultAsync(o => o.Id == id && o.Type == ObligationType.VendorPurchase, cancellationToken);
        if (obligation is null)
        {
            return false;
        }

        if (obligation.Status == ObligationStatus.Reversed)
        {
            throw Fail("id", "This purchase is already reversed.");
        }

        await ledger.ReverseAsync(PurchaseSource, id, reason, cancellationToken);

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

    private async Task<VendorPurchaseDto> ToDtoAsync(Obligation o, CancellationToken cancellationToken)
    {
        string vendorName = await db.Parties.AsNoTracking()
            .Where(p => p.Id == o.PartyId).Select(p => p.Name).FirstOrDefaultAsync(cancellationToken) ?? "";

        decimal partPaid = await db.Settlements.AsNoTracking()
            .Where(s => s.ObligationId == o.Id
                && s.Direction == SettlementDirection.Out
                && s.Status == SettlementStatus.Active)
            .SumAsync(s => (decimal?)s.Amount, cancellationToken) ?? 0m;

        decimal outstanding = await VendorOutstandingAsync(o.PartyId!.Value, cancellationToken);

        var lines = o.Lines
            .OrderBy(l => l.Id)
            .Select(l => new PurchaseLineDto(
                l.Id, l.ItemId, l.ItemName, l.Quantity, l.Unit, l.Rate, l.TaxAmount, l.LineTotal))
            .ToList();

        return new VendorPurchaseDto(
            o.Id, o.ProjectId, o.PartyId!.Value, vendorName, o.Date, o.Reference,
            o.Amount, partPaid, outstanding, o.Status.ToString(), lines);
    }

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
