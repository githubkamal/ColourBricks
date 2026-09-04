namespace ColourBricks.Application.Reporting.Framework;

/// <summary>The BRD §57 date presets, plus the Indian FY presets (plan.md §5.5).</summary>
public enum ReportDatePreset
{
    Custom = 0,
    Today,
    Yesterday,
    ThisWeek,
    PreviousWeek,
    ThisMonth,
    PreviousMonth,
    CurrentYear,
    PreviousYear,
    ThisFinancialYear,
    PreviousFinancialYear,
}

/// <summary>A resolved (or partly open) date window.</summary>
public sealed record DateRange(DateOnly? From, DateOnly? To);

/// <summary>
/// Every BRD §57 filter dimension a report may accept. A report declares which of
/// these it honours; the executor ignores the rest (plan.md §7, P8-T01).
/// </summary>
[Flags]
public enum ReportFilters
{
    None = 0,
    Date = 1 << 0,
    Project = 1 << 1,
    Vendor = 1 << 2,
    Subcontractor = 1 << 3,
    Department = 1 << 4,
    Item = 1 << 5,
    Category = 1 << 6,
    PaymentMode = 1 << 7,
    Account = 1 << 8,
    PaymentStatus = 1 << 9,
    TransactionType = 1 << 10,
    ReconciliationStatus = 1 << 11,
    Search = 1 << 12,
    All = Date | Project | Vendor | Subcontractor | Department | Item | Category
        | PaymentMode | Account | PaymentStatus | TransactionType | ReconciliationStatus | Search,
}

/// <summary>
/// The one query contract every report shares (plan.md §7). Bound straight from the
/// query string; unknown fields are simply not set.
/// </summary>
public sealed record ReportFilter
{
    public ReportDatePreset DatePreset { get; init; } = ReportDatePreset.Custom;
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }

    public long? ProjectId { get; init; }
    public long? VendorId { get; init; }
    public long? SubcontractorId { get; init; }
    public long? DepartmentId { get; init; }
    public long? ItemId { get; init; }
    public long? CategoryId { get; init; }
    public long? PaymentModeId { get; init; }
    public long? AccountId { get; init; }

    public string? PaymentStatus { get; init; }
    public string? TransactionType { get; init; }
    public string? ReconciliationStatus { get; init; }

    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public string? SortDir { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
