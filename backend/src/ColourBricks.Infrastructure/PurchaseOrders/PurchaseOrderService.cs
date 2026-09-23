using ColourBricks.Application.PurchaseOrders;
using ColourBricks.Application.VendorPurchases;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.PurchaseOrders;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.PurchaseOrders;

/// <summary>
/// Client request (2026-09-04): a vendor purchase order raised as a Draft — items
/// and quantities per project, no price — and submitted once the vendor's invoice
/// arrives. Submitting reuses <see cref="IVendorPurchaseService"/> once per project
/// involved, so an order that spans several projects reaches each project's
/// expenses and the vendor's outstanding through the exact path an ordinary vendor
/// purchase does (plan.md §5.1, §5.3) — nothing bespoke to keep in sync.
/// </summary>
public sealed class PurchaseOrderService(
    AppDbContext db,
    IVendorPurchaseService vendorPurchases,
    TimeProvider clock) : IPurchaseOrderService
{
    public async Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken ct)
    {
        if (!await db.Parties.AnyAsync(p => p.Id == request.VendorId, ct))
        {
            throw Fail("vendorId", "The vendor does not exist.");
        }

        List<PurchaseOrderLine> lines = await BuildLinesAsync(request.Lines, ct);

        var po = new PurchaseOrder
        {
            PoNumber = await NextNumberAsync(ct),
            VendorId = request.VendorId,
            OrderDate = request.OrderDate,
            Status = PurchaseOrderStatus.Draft,
            Notes = request.Notes?.Trim(),
        };
        po.Lines.AddRange(lines);

        db.Set<PurchaseOrder>().Add(po);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(po.Id, ct))!;
    }

    public async Task<PurchaseOrderDto?> GetAsync(long id, CancellationToken ct)
    {
        PurchaseOrder? po = await db.Set<PurchaseOrder>().AsNoTracking()
            .Include(p => p.Lines).Include(p => p.Charges)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return po is null ? null : await ToDtoAsync(po, ct);
    }

    public async Task<IReadOnlyList<PurchaseOrderDto>> ListAsync(long? vendorId, string? status, CancellationToken ct)
    {
        IQueryable<PurchaseOrder> query = db.Set<PurchaseOrder>().AsNoTracking()
            .Include(p => p.Lines).Include(p => p.Charges);

        if (vendorId is { } v)
        {
            query = query.Where(p => p.VendorId == v);
        }

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse(status, ignoreCase: true, out PurchaseOrderStatus parsed))
        {
            query = query.Where(p => p.Status == parsed);
        }

        List<PurchaseOrder> rows = await query
            .OrderByDescending(p => p.OrderDate).ThenByDescending(p => p.Id)
            .ToListAsync(ct);

        var dtos = new List<PurchaseOrderDto>(rows.Count);
        foreach (PurchaseOrder row in rows)
        {
            dtos.Add(await ToDtoAsync(row, ct));
        }

        return dtos;
    }

    public async Task<PurchaseOrderDto?> UpdateAsync(long id, UpdatePurchaseOrderRequest request, CancellationToken ct)
    {
        PurchaseOrder? po = await db.Set<PurchaseOrder>().Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (po is null)
        {
            return null;
        }

        if (po.Status != PurchaseOrderStatus.Draft)
        {
            throw Fail("id", "Only a draft order can be edited.");
        }

        db.Entry(po).Property(x => x.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;

        List<PurchaseOrderLine> lines = await BuildLinesAsync(request.Lines, ct);
        db.Set<PurchaseOrderLine>().RemoveRange(po.Lines);
        po.Lines.Clear();
        po.Lines.AddRange(lines);
        po.OrderDate = request.OrderDate;
        po.Notes = request.Notes?.Trim();

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PurchaseOrderDto> SubmitAsync(long id, SubmitPurchaseOrderRequest request, CancellationToken ct)
    {
        PurchaseOrder? po = await db.Set<PurchaseOrder>()
            .Include(p => p.Lines).Include(p => p.Charges)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (po is null)
        {
            throw Fail("id", "The purchase order does not exist.");
        }

        if (po.Status != PurchaseOrderStatus.Draft)
        {
            throw Fail("id", "Only a draft order can be submitted.");
        }

        if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
        {
            throw Fail("invoiceNumber", "The vendor's invoice number is required.");
        }

        // The whole order submits at once — every line must be priced (client request, 2026-09-04).
        Dictionary<long, SubmitPurchaseOrderLineInput> byLineId = request.Lines.ToDictionary(l => l.LineId);
        if (request.Lines.Count != po.Lines.Count || po.Lines.Any(l => !byLineId.ContainsKey(l.Id)))
        {
            throw Fail("lines", "Every line on the order must be priced.");
        }

        foreach (PurchaseOrderLine line in po.Lines)
        {
            SubmitPurchaseOrderLineInput input = byLineId[line.Id];
            if (input.Quantity <= 0m)
            {
                throw Fail("lines", "Quantity must be greater than zero.");
            }

            if (input.Rate < 0m)
            {
                throw Fail("lines", "Rate cannot be negative.");
            }

            if (input.TaxAmount < 0m)
            {
                throw Fail("lines", "Tax cannot be negative.");
            }

            decimal subtotal = input.Quantity * input.Rate;
            decimal taxAmount;
            if (input.TaxType == PurchaseOrderTaxType.Percentage)
            {
                if (input.TaxRate is not { } rate || rate < 0m)
                {
                    throw Fail("lines", "A GST percentage is required when tax is entered as a percentage.");
                }

                taxAmount = Money.Round(subtotal * rate / 100m);
            }
            else
            {
                taxAmount = input.TaxAmount;
            }

            line.Quantity = input.Quantity;
            line.Rate = input.Rate;
            line.TaxType = input.TaxType;
            line.TaxRate = input.TaxType == PurchaseOrderTaxType.Percentage ? input.TaxRate : null;
            line.TaxAmount = taxAmount;
            line.LineTotal = Money.Round(subtotal + taxAmount);
        }

        List<PurchaseOrderCharge> charges = BuildCharges(request.Charges);
        db.Set<PurchaseOrderCharge>().RemoveRange(po.Charges);
        po.Charges.Clear();
        po.Charges.AddRange(charges);
        po.RoundOff = Money.Round(request.RoundOff);

        // An extra charge is raised once for the whole invoice but the order may span
        // several projects, so each project carries the share of transport/handling its
        // own goods caused (client request, 2026-09-23). Apportioned by item value, with
        // the last project absorbing the rounding remainder so the shares always add back
        // up to the charge. The round-off, being an adjustment of a few paise, is not
        // split at all - it lands whole on the project with the largest share.
        var groups = po.Lines.GroupBy(l => l.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                Lines = g.ToList(),
                Total = Money.Round(g.Sum(l => l.LineTotal!.Value)),
            })
            .OrderByDescending(g => g.Total).ThenBy(g => g.ProjectId)
            .ToList();

        decimal linesGrandTotal = Money.Round(groups.Sum(g => g.Total));
        Dictionary<long, List<PurchaseLineInput>> chargeLines = ApportionCharges(
            charges, groups.Select(g => (g.ProjectId, g.Total)).ToList(), linesGrandTotal);

        long roundOffProjectId = groups[0].ProjectId;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var obligationIds = new List<long>();
        foreach (var group in groups)
        {
            var lines = group.Lines
                .Select(l => new PurchaseLineInput(l.ItemId, l.ItemName, l.Quantity, l.Unit, l.Rate!.Value, l.TaxAmount!.Value))
                .ToList();
            lines.AddRange(chargeLines.GetValueOrDefault(group.ProjectId, []));

            decimal roundOff = group.ProjectId == roundOffProjectId ? po.RoundOff : 0m;
            decimal groupTotal = Money.Round(
                lines.Sum(l => Money.Round(l.Quantity * l.Rate + l.TaxAmount)) + roundOff);

            RecordVendorPurchaseResult result = await vendorPurchases.RecordAsync(new RecordVendorPurchaseRequest(
                group.ProjectId, po.VendorId, po.OrderDate, groupTotal, lines,
                request.InvoiceNumber.Trim(), $"From purchase order {po.PoNumber}", RoundOff: roundOff), ct);
            obligationIds.Add(result.Purchase.Id);
        }

        // Stamp the trail back from each created vendor purchase to this order.
        List<Obligation> created = await db.Obligations.Where(o => obligationIds.Contains(o.Id)).ToListAsync(ct);
        foreach (Obligation o in created)
        {
            o.PurchaseOrderId = po.Id;
        }

        po.Status = PurchaseOrderStatus.Submitted;
        po.InvoiceNumber = request.InvoiceNumber.Trim();
        po.SubmittedDate = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
        return (await GetAsync(po.Id, ct))!;
    }

    public async Task<bool> CancelAsync(long id, CancellationToken ct)
    {
        PurchaseOrder? po = await db.Set<PurchaseOrder>().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (po is null)
        {
            return false;
        }

        if (po.Status != PurchaseOrderStatus.Draft)
        {
            throw Fail("id", "Only a draft order can be cancelled.");
        }

        po.Status = PurchaseOrderStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<List<PurchaseOrderLine>> BuildLinesAsync(
        IReadOnlyList<PurchaseOrderLineInput> inputs, CancellationToken ct)
    {
        if (inputs.Count == 0)
        {
            throw Fail("lines", "At least one line is required.");
        }

        List<long> projectIds = inputs.Select(l => l.ProjectId).Distinct().ToList();
        List<long> existingProjectIds = await db.Projects.AsNoTracking()
            .Where(p => projectIds.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        List<long> missing = projectIds.Except(existingProjectIds).ToList();
        if (missing.Count > 0)
        {
            throw Fail("lines", $"Project(s) {string.Join(", ", missing)} do not exist.");
        }

        var lines = new List<PurchaseOrderLine>(inputs.Count);
        foreach (PurchaseOrderLineInput input in inputs)
        {
            if (input.Quantity <= 0m)
            {
                throw Fail("lines", "Quantity must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(input.ItemName))
            {
                throw Fail("lines", "Item name is required.");
            }

            if (string.IsNullOrWhiteSpace(input.Unit))
            {
                throw Fail("lines", "Unit is required.");
            }

            lines.Add(new PurchaseOrderLine
            {
                ProjectId = input.ProjectId,
                ItemId = input.ItemId,
                ItemName = input.ItemName.Trim(),
                Quantity = input.Quantity,
                Unit = input.Unit.Trim(),
            });
        }

        return lines;
    }

    private static List<PurchaseOrderCharge> BuildCharges(IReadOnlyList<PurchaseOrderChargeInput>? inputs)
    {
        var charges = new List<PurchaseOrderCharge>();
        foreach (PurchaseOrderChargeInput input in inputs ?? [])
        {
            if (string.IsNullOrWhiteSpace(input.ChargeType))
            {
                throw Fail("charges", "Each extra charge needs a type (e.g. Transport).");
            }

            if (input.Amount < 0m)
            {
                throw Fail("charges", "A charge cannot be negative.");
            }

            decimal taxAmount;
            if (input.TaxType == PurchaseOrderTaxType.Percentage)
            {
                if (input.TaxRate is not { } rate || rate < 0m)
                {
                    throw Fail("charges", "A GST percentage is required when a charge's tax is entered as a percentage.");
                }

                taxAmount = Money.Round(input.Amount * rate / 100m);
            }
            else
            {
                if (input.TaxAmount < 0m)
                {
                    throw Fail("charges", "A charge's tax cannot be negative.");
                }

                taxAmount = Money.Round(input.TaxAmount);
            }

            charges.Add(new PurchaseOrderCharge
            {
                ChargeType = input.ChargeType.Trim(),
                Amount = Money.Round(input.Amount),
                TaxType = input.TaxType,
                TaxRate = input.TaxType == PurchaseOrderTaxType.Percentage ? input.TaxRate : null,
                TaxAmount = taxAmount,
                Total = Money.Round(input.Amount + taxAmount),
            });
        }

        return charges;
    }

    /// <summary>
    /// Splits each order-wide charge across the projects on the order in proportion
    /// to their item value, as one extra purchase line per project per charge. A
    /// share that rounds to nothing is dropped rather than posted as a zero line.
    /// </summary>
    /// <param name="groups">
    /// The projects on the order with their item value, <b>largest first</b>. The
    /// largest takes the rounding remainder — every other project takes its rounded
    /// share and the biggest absorbs what is left, so the shares always add back up
    /// to the charge exactly and the remainder can never come out negative (which a
    /// line's Rate is not allowed to be).
    /// </param>
    private static Dictionary<long, List<PurchaseLineInput>> ApportionCharges(
        IReadOnlyList<PurchaseOrderCharge> charges,
        IReadOnlyList<(long ProjectId, decimal Total)> groups,
        decimal linesGrandTotal)
    {
        var result = new Dictionary<long, List<PurchaseLineInput>>();
        if (charges.Count == 0 || groups.Count == 0)
        {
            return result;
        }

        void Add(long projectId, PurchaseOrderCharge charge, decimal amount, decimal tax)
        {
            if (amount == 0m && tax == 0m)
            {
                return;
            }

            if (!result.TryGetValue(projectId, out List<PurchaseLineInput>? lines))
            {
                result[projectId] = lines = [];
            }

            lines.Add(new PurchaseLineInput(null, charge.ChargeType, 1m, "lot", amount, tax));
        }

        foreach (PurchaseOrderCharge charge in charges)
        {
            decimal amountLeft = charge.Amount;
            decimal taxLeft = charge.TaxAmount;

            for (int i = 1; i < groups.Count; i++)
            {
                (long projectId, decimal groupTotal) = groups[i];
                // An order whose every line is priced at zero has no value to weight
                // by, so fall back to an equal split rather than dividing by zero.
                decimal share = linesGrandTotal == 0m
                    ? 1m / groups.Count
                    : groupTotal / linesGrandTotal;

                decimal amount = Money.Round(charge.Amount * share);
                decimal tax = Money.Round(charge.TaxAmount * share);
                amountLeft -= amount;
                taxLeft -= tax;
                Add(projectId, charge, amount, tax);
            }

            Add(groups[0].ProjectId, charge, amountLeft, taxLeft);
        }

        return result;
    }

    private async Task<string> NextNumberAsync(CancellationToken ct)
    {
        int year = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).Year;
        string prefix = $"PO-{year}-";
        int count = await db.Set<PurchaseOrder>().CountAsync(p => p.PoNumber.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:D5}";
    }

    private async Task<PurchaseOrderDto> ToDtoAsync(PurchaseOrder po, CancellationToken ct)
    {
        string vendorName = await db.Parties.AsNoTracking()
            .Where(p => p.Id == po.VendorId).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? "";

        List<long> projectIds = po.Lines.Select(l => l.ProjectId).Distinct().ToList();
        Dictionary<long, string> projectNames = await db.Projects.AsNoTracking()
            .Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        List<long> obligationIds = await db.Obligations.AsNoTracking()
            .Where(o => o.PurchaseOrderId == po.Id).Select(o => o.Id).ToListAsync(ct);

        var lines = po.Lines
            .OrderBy(l => l.Id)
            .Select(l => new PurchaseOrderLineDto(
                l.Id, l.ProjectId, projectNames.GetValueOrDefault(l.ProjectId, ""), l.ItemId, l.ItemName,
                l.Quantity, l.Unit, l.Rate, l.Rate is { } r ? Money.Round(l.Quantity * r) : null,
                l.TaxType, l.TaxRate, l.TaxAmount, l.LineTotal))
            .ToList();

        var charges = po.Charges
            .OrderBy(c => c.Id)
            .Select(c => new PurchaseOrderChargeDto(
                c.Id, c.ChargeType, c.Amount, c.TaxType, c.TaxRate, c.TaxAmount, c.Total))
            .ToList();

        decimal subtotalTotal = Money.Round(lines.Sum(l => l.Subtotal ?? 0m));
        decimal taxTotal = Money.Round(lines.Sum(l => l.TaxAmount ?? 0m));
        decimal chargesSubtotal = Money.Round(charges.Sum(c => c.Amount));
        decimal chargesTax = Money.Round(charges.Sum(c => c.TaxAmount));
        decimal total = Money.Round(
            lines.Sum(l => l.LineTotal ?? 0m) + charges.Sum(c => c.Total) + po.RoundOff);

        return new PurchaseOrderDto(
            po.Id, po.PoNumber, po.VendorId, vendorName, po.OrderDate, po.Status.ToString(),
            po.InvoiceNumber, po.SubmittedDate, po.Notes,
            subtotalTotal, taxTotal, total,
            lines, charges, chargesSubtotal, chargesTax, po.RoundOff,
            obligationIds, po.ConcurrencyStamp);
    }

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
