using ColourBricks.Application.Loans;
using ColourBricks.Application.Notifications;
using ColourBricks.Application.Outstanding;
using ColourBricks.Application.Reporting;
using ColourBricks.Application.Settings;
using ColourBricks.Domain.Banking;
using ColourBricks.Domain.Notifications;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Projects;
using ColourBricks.Domain.Services;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ColourBricks.Infrastructure.Notifications;

/// <summary>
/// P9-T01 — evaluates the ten BRD §66 triggers. Idempotent: a notification exists
/// once per <see cref="Notification.DedupeKey"/>, so re-running raises nothing new
/// for an unchanged situation. Email delivery is per-notification try/catch — a
/// failed send is recorded and the job carries on.
/// </summary>
public sealed class NotificationEvaluator(
    AppDbContext db,
    IReportingService reporting,
    ILoanAlertService loanAlerts,
    IOutstandingService outstanding,
    ISystemSettingsService systemSettings,
    IEmailSender email,
    TimeProvider clock,
    ILogger<NotificationEvaluator> logger) : INotificationEvaluator
{
    public async Task<NotificationRunResult> RunAsync(CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        // Was hardcoded (500,000 / 30 days / 10% / 7 days) — now the admin-configurable
        // System Settings defaults (client-confirmed scope, 2026-09-04).
        SystemSettingsDto settings = await systemSettings.GetAsync(ct);
        var candidates = new List<Notification>();

        await BudgetAsync(candidates, ct);
        await PendingReconciliationAsync(candidates, ct);
        await BankAllocationMismatchAsync(candidates, ct);
        await EmiAsync(candidates, settings.LoanEmiReminderDaysAhead, ct);
        await OverduePaymentsAsync(candidates, today, settings.OverdueAlertDays, ct);
        await VendorOutstandingAsync(candidates, settings.VendorOutstandingAlertLimit, ct);
        await ProfitabilityAsync(candidates, today, settings.ProfitFloorAlertPercent, ct);

        HashSet<string> existing = (await db.Set<Notification>()
                .Select(n => n.DedupeKey)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        List<Notification> fresh = candidates
            .GroupBy(n => n.DedupeKey, StringComparer.Ordinal)
            .Select(g => g.First())
            .Where(n => !existing.Contains(n.DedupeKey))
            .ToList();

        if (fresh.Count == 0)
        {
            return new NotificationRunResult(0, 0, 0);
        }

        db.Set<Notification>().AddRange(fresh);
        await db.SaveChangesAsync(ct);

        (int sent, int failed) = await DeliverEmailsAsync(fresh, ct);
        return new NotificationRunResult(fresh.Count, sent, failed);
    }

    // ── triggers ─────────────────────────────────────────────────────────────

    private async Task BudgetAsync(List<Notification> into, CancellationToken ct)
    {
        List<long> projectIds = await db.Projects.AsNoTracking()
            .Where(p => p.Status == ProjectStatus.Ongoing).Select(p => p.Id).ToListAsync(ct);

        foreach (long projectId in projectIds)
        {
            var dto = await reporting.BudgetVsActualAsync(projectId, ct);
            bool exceeded = dto.ActualCost > dto.EstimatedCost && dto.EstimatedCost > 0m
                || dto.Rows.Any(r => r.Status == "Exceeded");
            bool approaching = !exceeded && dto.Rows.Any(r => r.Status == "Approaching");

            int revision = dto.BudgetRevisionNumber ?? 0;
            if (exceeded)
            {
                into.Add(Make(NotificationTrigger.BudgetExceeded, $"budget_exceeded:{projectId}:{revision}",
                    "Project budget exceeded",
                    $"Project #{projectId} actual cost {dto.ActualCost:0.00} has exceeded its budget.",
                    "Critical", projectId, "Project", projectId));
            }
            else if (approaching)
            {
                into.Add(Make(NotificationTrigger.BudgetApproaching, $"budget_approaching:{projectId}:{revision}",
                    "Project approaching budget",
                    $"Project #{projectId} is close to its budget in one or more categories.",
                    "Warning", projectId, "Project", projectId));
            }
        }
    }

    private async Task PendingReconciliationAsync(List<Notification> into, CancellationToken ct)
    {
        var batches = await (
            from t in db.BankTransactions.AsNoTracking()
            where t.Status == BankTransactionStatus.Pending || t.Status == BankTransactionStatus.InReview
            group t by t.ImportBatchId into g
            select new { BatchId = g.Key, Count = g.Count() }).ToListAsync(ct);

        foreach (var batch in batches)
        {
            into.Add(Make(NotificationTrigger.PendingReconciliation, $"pending_reconciliation:batch:{batch.BatchId}",
                "Bank transactions pending reconciliation",
                $"Import batch #{batch.BatchId} has {batch.Count} transaction(s) awaiting reconciliation.",
                "Info", null, "ImportBatch", batch.BatchId));
        }
    }

    private async Task BankAllocationMismatchAsync(List<Notification> into, CancellationToken ct)
    {
        // A bank transaction can now carry several links (a split map across a vendor and/or
        // Personal/Office/Savings — client request, 2026-09-04), so sum every active link's
        // amount per transaction rather than assume exactly one.
        var settlementAmounts =
            from l in db.ReconciliationLinks.AsNoTracking()
            where l.UnlinkedAtUtc == null
                && (l.Kind == ReconciliationLinkKind.Settlement || l.Kind == ReconciliationLinkKind.CustomWorkPayment)
            join s in db.Settlements.AsNoTracking() on l.SettlementId!.Value equals s.Id
            select new { l.BankTransactionId, Amount = s.Amount };

        var commonExpenseAmounts =
            from l in db.ReconciliationLinks.AsNoTracking()
            where l.UnlinkedAtUtc == null && l.Kind == ReconciliationLinkKind.CommonExpense
            join e in db.Set<ColourBricks.Domain.CommonExpenses.CommonExpense>().AsNoTracking()
                on l.CommonExpenseId!.Value equals e.Id
            select new { l.BankTransactionId, Amount = e.Amount };

        var directExpenseAmounts =
            from l in db.ReconciliationLinks.AsNoTracking()
            where l.UnlinkedAtUtc == null && l.Kind == ReconciliationLinkKind.DirectExpense
            join o in db.Obligations.AsNoTracking() on l.ObligationId!.Value equals o.Id
            select new { l.BankTransactionId, Amount = o.Amount };

        var linked = await settlementAmounts.Concat(commonExpenseAmounts).Concat(directExpenseAmounts).ToListAsync(ct);
        var linkedByTx = linked.GroupBy(x => x.BankTransactionId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var reconciled = await db.BankTransactions.AsNoTracking()
            .Where(t => t.Status == BankTransactionStatus.Reconciled)
            .Select(t => new { t.Id, Bank = t.Debit + t.Credit })
            .ToListAsync(ct);

        var mismatches = reconciled
            .Select(t => new { t.Id, t.Bank, Settlement = linkedByTx.GetValueOrDefault(t.Id, 0m) });

        foreach (var m in mismatches.Where(x => Math.Abs(x.Bank - x.Settlement) > Money.Tolerance))
        {
            into.Add(Make(NotificationTrigger.BankAllocationMismatch, $"bank_allocation_mismatch:tx:{m.Id}",
                "Bank amount does not match project allocation",
                $"Bank transaction #{m.Id} ({m.Bank:0.00}) differs from its allocation ({m.Settlement:0.00}).",
                "Critical", null, "BankTransaction", m.Id));
        }
    }

    private async Task EmiAsync(List<Notification> into, int emiReminderDaysAhead, CancellationToken ct)
    {
        LoanAlertsDto alerts = await loanAlerts.RunAsync(emiReminderDaysAhead, ct);
        foreach (LoanAlertDto a in alerts.Upcoming)
        {
            into.Add(Make(NotificationTrigger.EmiDue, $"emi_due:instalment:{a.InstalmentId}",
                "EMI due soon", $"Loan #{a.LoanId} instalment {a.InstalmentNo} of {a.Amount:0.00} is due {a.DueDate:yyyy-MM-dd}.",
                "Warning", null, "LoanEmiInstalment", a.InstalmentId));
        }

        foreach (LoanAlertDto a in alerts.Overdue)
        {
            into.Add(Make(NotificationTrigger.EmiOverdue, $"emi_overdue:instalment:{a.InstalmentId}",
                "EMI overdue", $"Loan #{a.LoanId} instalment {a.InstalmentNo} of {a.Amount:0.00} was due {a.DueDate:yyyy-MM-dd}.",
                "Critical", null, "LoanEmiInstalment", a.InstalmentId));
        }
    }

    private async Task OverduePaymentsAsync(
        List<Notification> into, DateOnly today, int overdueDays, CancellationToken ct)
    {
        DateOnly cutoff = today.AddDays(-overdueDays);
        var obligations = await (
            from o in db.Obligations.AsNoTracking()
            where o.Status == ObligationStatus.Active
                && (o.Type == ObligationType.VendorPurchase || o.Type == ObligationType.SubcontractorWork)
                && o.Date < cutoff && o.PartyId != null
            select new { o.Id, o.Type, o.PartyId, o.Amount }).ToListAsync(ct);

        foreach (var group in obligations.GroupBy(x => (x.PartyId!.Value, x.Type)))
        {
            bool vendor = group.Key.Type == ObligationType.VendorPurchase;
            decimal owed = vendor
                ? await outstanding.VendorTotalAsync(group.Key.Item1, ct)
                : await outstanding.SubcontractorTotalAsync(group.Key.Item1, ct);
            if (owed <= 0m)
            {
                continue;
            }

            string trigger = vendor
                ? NotificationTrigger.VendorPaymentOverdue
                : NotificationTrigger.SubcontractorPaymentOverdue;
            into.Add(Make(trigger, $"{trigger}:party:{group.Key.Item1}:{today:yyyy-MM}",
                vendor ? "Vendor payment overdue" : "Subcontractor payment overdue",
                $"Party #{group.Key.Item1} has {owed:0.00} outstanding on obligations older than {overdueDays} days.",
                "Warning", null, "Party", group.Key.Item1));
        }
    }

    private async Task VendorOutstandingAsync(
        List<Notification> into, decimal vendorOutstandingLimit, CancellationToken ct)
    {
        List<long> vendorIds = await db.Parties.AsNoTracking()
            .Where(p => p.Types.HasFlag(PartyType.Vendor)).Select(p => p.Id).ToListAsync(ct);

        foreach (long vendorId in vendorIds)
        {
            decimal owed = await outstanding.VendorTotalAsync(vendorId, ct);
            if (owed < vendorOutstandingLimit)
            {
                continue;
            }

            long band = (long)(owed / vendorOutstandingLimit);
            into.Add(Make(NotificationTrigger.VendorOutstandingLimit, $"vendor_outstanding_limit:{vendorId}:{band}",
                "Vendor outstanding limit reached",
                $"Vendor #{vendorId} outstanding {owed:0.00} has reached the {vendorOutstandingLimit:0} limit.",
                "Warning", null, "Party", vendorId));
        }
    }

    private async Task ProfitabilityAsync(
        List<Notification> into, DateOnly today, decimal profitFloorPercent, CancellationToken ct)
    {
        List<long> projectIds = await db.Projects.AsNoTracking()
            .Where(p => p.Status == ProjectStatus.Ongoing).Select(p => p.Id).ToListAsync(ct);

        foreach (long projectId in projectIds)
        {
            var pnl = await reporting.ProjectPnlAsync(projectId, "Receipts", ct);
            if (pnl.Revenue <= 0m || pnl.ProfitPercent >= profitFloorPercent)
            {
                continue;
            }

            into.Add(Make(NotificationTrigger.ProfitabilityBelowThreshold,
                $"profitability_below_threshold:{projectId}:{today:yyyy-MM}",
                "Project profitability below threshold",
                $"Project #{projectId} profit is {pnl.ProfitPercent:0.0}% (below {profitFloorPercent}%).",
                "Warning", projectId, "Project", projectId));
        }
    }

    // ── email delivery ───────────────────────────────────────────────────────

    private async Task<(int Sent, int Failed)> DeliverEmailsAsync(List<Notification> fresh, CancellationToken ct)
    {
        var recipients = await (
            from u in db.Users.AsNoTracking()
            where u.RoleId != null && u.Email != ""
            select new { u.Email, u.RoleId }).ToListAsync(ct);

        List<NotificationChannelConfig> configs = await db.Set<NotificationChannelConfig>().AsNoTracking().ToListAsync(ct);

        int sent = 0, failed = 0;
        foreach (Notification notification in fresh)
        {
            List<string> addresses = recipients
                .Where(r => ChannelFor(configs, r.RoleId!.Value, notification.Trigger) is NotificationChannel.Email or NotificationChannel.Both)
                .Select(r => r.Email)
                .Distinct()
                .ToList();

            if (addresses.Count == 0)
            {
                continue;
            }

            notification.EmailAttempts++;
            try
            {
                foreach (string address in addresses)
                {
                    await email.SendAsync(address, notification.Title, notification.Body, ct);
                }

                notification.EmailSentAtUtc = clock.GetUtcNow();
                notification.EmailError = null;
                sent += addresses.Count;
            }
            catch (Exception ex)
            {
                notification.EmailError = ex.Message;
                failed += addresses.Count;
                logger.LogWarning(ex, "Notification {Id} email delivery failed (attempt {Attempt}).",
                    notification.Id, notification.EmailAttempts);
            }
        }

        await db.SaveChangesAsync(ct);
        return (sent, failed);
    }

    private static NotificationChannel ChannelFor(List<NotificationChannelConfig> configs, long roleId, string trigger) =>
        configs.FirstOrDefault(c => c.RoleId == roleId && c.Trigger == trigger)?.Channel ?? NotificationChannel.Dashboard;

    private Notification Make(
        string trigger, string dedupeKey, string title, string body, string severity,
        long? projectId, string entityType, long entityId) => new()
    {
        Trigger = trigger,
        DedupeKey = dedupeKey,
        Title = title,
        Body = body,
        Severity = severity,
        ProjectId = projectId,
        EntityType = entityType,
        EntityId = entityId,
    };
}
