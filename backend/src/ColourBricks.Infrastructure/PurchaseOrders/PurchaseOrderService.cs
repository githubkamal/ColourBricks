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
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return po is null ? null : await ToDtoAsync(po, ct);
    }

    public async Task<IReadOnlyList<PurchaseOrderDto>> ListAsync(long? vendorId, string? status, CancellationToken ct)
    {
        IQueryable<PurchaseOrder> query = db.Set<PurchaseOrder>().AsNoTracking().Include(p => p.Lines);

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
        PurchaseOrder? po = await db.Set<PurchaseOrder>().Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id, ct);
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

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var obligationIds = new List<long>();
        foreach (IGrouping<long, PurchaseOrderLine> group in po.Lines.GroupBy(l => l.ProjectId))
        {
            decimal groupTotal = Money.Round(group.Sum(l => l.LineTotal!.Value));
            var lines = group
                .Select(l => new PurchaseLineInput(l.ItemId, l.ItemName, l.Quantity, l.Unit, l.Rate!.Value, l.TaxAmount!.Value))
                .ToList();

            RecordVendorPurchaseResult result = await vendorPurchases.RecordAsync(new RecordVendorPurchaseRequest(
                group.Key, po.VendorId, po.OrderDate, groupTotal, lines,
                request.InvoiceNumber.Trim(), $"From purchase order {po.PoNumber}"), ct);
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

        decimal subtotalTotal = Money.Round(lines.Sum(l => l.Subtotal ?? 0m));
        decimal taxTotal = Money.Round(lines.Sum(l => l.TaxAmount ?? 0m));

        return new PurchaseOrderDto(
            po.Id, po.PoNumber, po.VendorId, vendorName, po.OrderDate, po.Status.ToString(),
            po.InvoiceNumber, po.SubmittedDate, po.Notes,
            subtotalTotal, taxTotal, Money.Round(lines.Sum(l => l.LineTotal ?? 0m)),
            lines, obligationIds, po.ConcurrencyStamp);
    }

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
