using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Settings;

/// <summary>
/// A single-row table (BRD's "System Settings" nav item, unspecified beyond the
/// label — built against client-confirmed scope, 2026-09-04): the company profile
/// shown on report headers/exports/printed documents, and the notification
/// thresholds previously hardcoded in <c>NotificationEvaluator</c> and the loans
/// EMI-alert lookahead window. Always exactly one row; <c>ISystemSettingsService</c>
/// creates it with these defaults on first read — its own <c>Id</c> is exposed on
/// the DTO precisely so the frontend can attach a logo upload to it via
/// <c>ownerType=SystemSettings</c>, the only place anything else looks it up by id.
/// </summary>
[Auditable("admin_configuration")]
public sealed class SystemSettings : BaseEntity
{
    // ── Company profile ──────────────────────────────────────────────────────

    public string CompanyName { get; set; } = "Colour Bricks";

    public string? CompanyAddress { get; set; }

    public string? CompanyGstin { get; set; }

    /// <summary>A pasted, externally hosted logo URL — the alternative to <see cref="CompanyLogoAttachmentId"/>.</summary>
    public string? CompanyLogoUrl { get; set; }

    /// <summary>
    /// A logo uploaded through the Attachment system (client-added, 2026-09-05) —
    /// the alternative to <see cref="CompanyLogoUrl"/>. Mutually exclusive with it
    /// at the UI level; if somehow both are set, the attachment wins (it is the
    /// higher-fidelity source — an actual file, not a link that can rot).
    /// </summary>
    public long? CompanyLogoAttachmentId { get; set; }

    // ── Notification defaults (BRD §66) ──────────────────────────────────────

    /// <summary>A vendor's outstanding balance alert fires above this (was a hardcoded 500,000).</summary>
    public decimal VendorOutstandingAlertLimit { get; set; } = 500_000m;

    /// <summary>An obligation this many days past due fires the overdue alert (was hardcoded 30).</summary>
    public int OverdueAlertDays { get; set; } = 30;

    /// <summary>A project's profit % below this fires the low-profitability alert (was hardcoded 10).</summary>
    public decimal ProfitFloorAlertPercent { get; set; } = 10m;

    /// <summary>
    /// How many days ahead of an EMI due date the reminder fires — the daily
    /// notification job's default (was hardcoded 7); also the default for the
    /// on-demand Loans &gt; Alerts endpoint when the caller doesn't override it.
    /// </summary>
    public int LoanEmiReminderDaysAhead { get; set; } = 7;
}
