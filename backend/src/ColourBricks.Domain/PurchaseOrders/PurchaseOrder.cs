using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.PurchaseOrders;

/// <summary>A draft is edited freely; submitting is one-way (client request, 2026-09-04). TINYINT.</summary>
public enum PurchaseOrderStatus : byte
{
    Draft = 1,
    Submitted = 2,
    Cancelled = 3,
}

/// <summary>
/// A vendor purchase order raised before the vendor's invoice arrives (client
/// request, 2026-09-04). Drafted with items and quantities per project — a single
/// order may span several projects. Submitting prices every line (and lets the
/// accountant correct quantity against what the invoice actually says) and creates
/// one ordinary vendor purchase — ColourBricks.Domain.Obligations.Obligation, the
/// same ledger posting as any other purchase — per project involved, so it reaches
/// project expenses and vendor outstanding through the existing path
/// (plan.md §5.1, §5.3). A draft never touches the ledger.
/// </summary>
[Auditable("materials")]
public sealed class PurchaseOrder : BaseEntity
{
    public required string PoNumber { get; set; }

    public long VendorId { get; set; }

    public DateOnly OrderDate { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    /// <summary>The vendor's invoice number, captured when the order is submitted.</summary>
    public string? InvoiceNumber { get; set; }

    public DateOnly? SubmittedDate { get; set; }

    public string? Notes { get; set; }

    public List<PurchaseOrderLine> Lines { get; } = [];
}

/// <summary>
/// One ordered item, scoped to a single project. <see cref="Rate"/>,
/// <see cref="TaxAmount"/> and <see cref="LineTotal"/> stay null until the order is
/// submitted — a draft records what was ordered, not what it costs.
/// </summary>
public sealed class PurchaseOrderLine : BaseEntity
{
    public long PurchaseOrderId { get; set; }

    public long ProjectId { get; set; }

    public long? ItemId { get; set; }

    public required string ItemName { get; set; }

    public decimal Quantity { get; set; }

    public required string Unit { get; set; }

    public decimal? Rate { get; set; }

    public decimal? TaxAmount { get; set; }

    public decimal? LineTotal { get; set; }
}
