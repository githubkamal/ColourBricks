using ColourBricks.Application.Integrity;
using ColourBricks.Application.Ledger;
using ColourBricks.Application.Outstanding;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Services;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Integrity;

/// <summary>
/// BRD §38's five reconciliation controls, run as independent recomputations from
/// the primary records (obligations, settlements, allocations, opening balances) and
/// compared against the ledger-derived figures the app serves. Any disagreement is a
/// double-count or a missing/duplicated posting. This is the plan.md §11 safety net.
/// </summary>
public sealed class IntegrityCheckService(
    AppDbContext db,
    ILedgerQueryService ledgerQuery,
    IOutstandingService outstanding,
    IExpenseCategoryService categories,
    TimeProvider clock) : IIntegrityCheckService
{
    private static readonly decimal Tolerance = Money.Tolerance;
    private const string MultiProjectDescription = "Vendor payment (multi-project)";

    public async Task<IntegrityCheckReport> RunAsync(CancellationToken cancellationToken)
    {
        var controls = new List<IntegrityControlResult>
        {
            await BankAsync(cancellationToken),
            await VendorAsync(cancellationToken),
            await ProjectAsync(cancellationToken),
            await ClientAsync(cancellationToken),
            await MultiProjectPaymentAsync(cancellationToken),
        };

        return new IntegrityCheckReport(
            controls.All(c => c.Passed), clock.GetUtcNow(), controls);
    }

    private async Task<IntegrityControlResult> BankAsync(CancellationToken ct)
    {
        const string formula = "Opening Balance + Credits − Debits = Closing Bank Balance";
        var violations = new List<IntegrityViolation>();

        var accounts = await db.Accounts.AsNoTracking()
            .Select(a => new { a.Id, a.Name, a.OpeningBalance })
            .ToListAsync(ct);

        Dictionary<long, decimal> ledgerNet = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.AccountId != null)
            .GroupBy(e => e.AccountId!.Value)
            .Select(g => new { AccountId = g.Key, Net = g.Sum(x => x.Credit - x.Debit) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Net, ct);

        var movement = await db.Settlements.AsNoTracking()
            .Where(s => s.AccountId != null && s.Status == SettlementStatus.Active)
            .GroupBy(s => new { AccountId = s.AccountId!.Value, s.Direction })
            .Select(g => new { g.Key.AccountId, g.Key.Direction, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        // Immediate direct-expense payments move cash with no settlement row; the only
        // primary figure is the obligation amount, located to an account by its ledger leg.
        Dictionary<long, decimal> directExpenseOut = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.AccountId != null && e.SourceType == "DirectExpense" && !e.IsReversal)
            .Join(db.Obligations.AsNoTracking().Where(o => o.Status == ObligationStatus.Active),
                e => e.SourceId, o => o.Id, (e, o) => new { AccountId = e.AccountId!.Value, o.Amount })
            .GroupBy(x => x.AccountId)
            .Select(g => new { AccountId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Amount, ct);

        // Internal transfers (P4-T08), company-level common expenses (P6-T01) and loan
        // disbursements / EMI payments (P7) move cash with no settlement row.
        Dictionary<long, decimal> transferNet = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.AccountId != null
                && (e.SourceType == "InternalTransfer" || e.SourceType == "CommonExpense"
                    || e.SourceType == "LoanDisbursement" || e.SourceType == "LoanEmiPayment"))
            .GroupBy(e => e.AccountId!.Value)
            .Select(g => new { AccountId = g.Key, Net = g.Sum(x => x.Credit - x.Debit) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Net, ct);

        foreach (var a in accounts)
        {
            decimal net = ledgerNet.GetValueOrDefault(a.Id, 0m);
            decimal served = await ledgerQuery.GetAccountBalanceAsync(a.Id, ct);

            decimal inflow = movement
                .Where(m => m.AccountId == a.Id && m.Direction == SettlementDirection.In)
                .Sum(m => m.Amount);
            decimal outflow = movement
                .Where(m => m.AccountId == a.Id && m.Direction == SettlementDirection.Out)
                .Sum(m => m.Amount);
            decimal movementNet = inflow - outflow
                - directExpenseOut.GetValueOrDefault(a.Id, 0m)
                + transferNet.GetValueOrDefault(a.Id, 0m);

            if (Math.Abs(served - (a.OpeningBalance + net)) > Tolerance
                || Math.Abs(movementNet - net) > Tolerance)
            {
                violations.Add(new IntegrityViolation(
                    "Account", a.Id, a.Name, a.OpeningBalance + movementNet, served,
                    $"ledger Σ(credit−debit) {net:0.00} vs primary movement {movementNet:0.00}"));
            }
        }

        return new IntegrityControlResult("Bank", formula, violations.Count == 0, violations);
    }

    private async Task<IntegrityControlResult> VendorAsync(CancellationToken ct)
    {
        const string formula = "Σ Purchases − Σ Payments = Vendor Outstanding";
        long payable = await categories.RequireIdAsync("vendor_payable", ct);
        var violations = new List<IntegrityViolation>();

        // A List (not a static string[]) so EF's parameter funcletizer translates the IN clause.
        var vendorPaymentDescriptions = new List<string>
        {
            "Vendor payment", MultiProjectDescription, "Inline part-payment",
        };

        Dictionary<long, decimal> purchases = await db.Obligations.AsNoTracking()
            .Where(o => o.Type == ObligationType.VendorPurchase
                && o.Status == ObligationStatus.Active && o.PartyId != null)
            .GroupBy(o => o.PartyId!.Value)
            .Select(g => new { PartyId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.PartyId, x => x.Amount, ct);

        Dictionary<long, decimal> payments = await db.Settlements.AsNoTracking()
            .Where(s => s.Direction == SettlementDirection.Out
                && s.Status == SettlementStatus.Active
                && s.PartyId != null
                && s.Description != null
                && vendorPaymentDescriptions.Contains(s.Description))
            .GroupBy(s => s.PartyId!.Value)
            .Select(g => new { PartyId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.PartyId, x => x.Amount, ct);

        Dictionary<long, decimal> served = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.CategoryId == payable && e.PartyId != null)
            .GroupBy(e => e.PartyId!.Value)
            .Select(g => new { PartyId = g.Key, Net = g.Sum(x => x.Credit - x.Debit) })
            .ToDictionaryAsync(x => x.PartyId, x => x.Net, ct);

        List<long> ids = purchases.Keys.Union(payments.Keys).Union(served.Keys).Distinct().ToList();
        Dictionary<long, string> names = await NamesAsync(ids, ct);

        foreach (long id in ids)
        {
            decimal primary = purchases.GetValueOrDefault(id, 0m) - payments.GetValueOrDefault(id, 0m);
            decimal ledger = served.GetValueOrDefault(id, 0m);
            if (Math.Abs(primary - ledger) > Tolerance)
            {
                violations.Add(new IntegrityViolation(
                    "Vendor", id, names.GetValueOrDefault(id, ""), primary, ledger,
                    $"purchases {purchases.GetValueOrDefault(id, 0m):0.00} − payments "
                    + $"{payments.GetValueOrDefault(id, 0m):0.00}"));
            }
        }

        return new IntegrityControlResult("Vendor", formula, violations.Count == 0, violations);
    }

    private async Task<IntegrityControlResult> ProjectAsync(CancellationToken ct)
    {
        const string formula = "Σ Project Payables − Σ Project Payments = Project Outstanding";
        var violations = new List<IntegrityViolation>();

        var payableCats = new List<long>
        {
            await categories.RequireIdAsync("vendor_payable", ct),
            await categories.RequireIdAsync("subcontractor_payable", ct),
            await categories.RequireIdAsync("custom_work_payable", ct),
            await categories.RequireIdAsync("temple_donation_payable", ct),
        };
        var payableTypes = new List<ObligationType>
        {
            ObligationType.VendorPurchase, ObligationType.SubcontractorWork,
            ObligationType.CustomWork, ObligationType.TempleDonation,
        };

        Dictionary<long, decimal> obligations = await db.Obligations.AsNoTracking()
            .Where(o => payableTypes.Contains(o.Type) && o.Status == ObligationStatus.Active)
            .GroupBy(o => o.ProjectId)
            .Select(g => new { ProjectId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Amount, ct);

        // The advance portion of a single-project over-payment carries the settlement's
        // ProjectId but never touches that project's payable (it books a party-level
        // credit with no project). Net it out of the project's direct payments.
        Dictionary<long, decimal> advanceBySettlement = await db.Allocations.AsNoTracking()
            .Where(a => a.SettlementId != null && a.ObligationId == null
                && a.ProjectId == null && a.Amount > 0m)
            .GroupBy(a => a.SettlementId!.Value)
            .Select(g => new { SettlementId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.SettlementId, x => x.Amount, ct);

        var outSettlements = await db.Settlements.AsNoTracking()
            .Where(s => s.Direction == SettlementDirection.Out
                && s.Status == SettlementStatus.Active && s.ProjectId != null)
            .Select(s => new { s.Id, ProjectId = s.ProjectId!.Value, s.Amount })
            .ToListAsync(ct);

        Dictionary<long, decimal> directPayments = outSettlements
            .GroupBy(s => s.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.Amount - advanceBySettlement.GetValueOrDefault(s.Id, 0m)));

        Dictionary<long, decimal> allocated = await db.Allocations.AsNoTracking()
            .Where(a => a.ProjectId != null
                && (a.SettlementId == null
                    || db.Settlements.Any(s => s.Id == a.SettlementId && s.Status == SettlementStatus.Active)))
            .GroupBy(a => a.ProjectId!.Value)
            .Select(g => new { ProjectId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Amount, ct);

        Dictionary<long, decimal> served = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.ProjectId != null && payableCats.Contains(e.CategoryId))
            .GroupBy(e => e.ProjectId!.Value)
            .Select(g => new { ProjectId = g.Key, Net = g.Sum(x => x.Credit - x.Debit) })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Net, ct);

        List<long> ids = obligations.Keys.Union(directPayments.Keys)
            .Union(allocated.Keys).Union(served.Keys).Distinct().ToList();
        Dictionary<long, string> names = await db.Projects.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        foreach (long id in ids)
        {
            decimal primary = obligations.GetValueOrDefault(id, 0m)
                - directPayments.GetValueOrDefault(id, 0m)
                - allocated.GetValueOrDefault(id, 0m);
            decimal ledger = served.GetValueOrDefault(id, 0m);
            if (Math.Abs(primary - ledger) > Tolerance)
            {
                violations.Add(new IntegrityViolation(
                    "Project", id, names.GetValueOrDefault(id, ""), primary, ledger,
                    $"payables {obligations.GetValueOrDefault(id, 0m):0.00} − direct "
                    + $"{directPayments.GetValueOrDefault(id, 0m):0.00} − allocated "
                    + $"{allocated.GetValueOrDefault(id, 0m):0.00}"));
            }
        }

        return new IntegrityControlResult("Project", formula, violations.Count == 0, violations);
    }

    private async Task<IntegrityControlResult> ClientAsync(CancellationToken ct)
    {
        const string formula = "Project Amount Due − Client Receipts = Client Outstanding";
        var violations = new List<IntegrityViolation>();

        var projects = await db.Projects.AsNoTracking()
            .Select(p => new { p.Id, p.Name, p.ContractValue })
            .ToListAsync(ct);

        Dictionary<long, decimal> receipts = await db.Settlements.AsNoTracking()
            .Where(s => s.Direction == SettlementDirection.In
                && s.Status == SettlementStatus.Active && s.ProjectId != null)
            .GroupBy(s => s.ProjectId!.Value)
            .Select(g => new { ProjectId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Amount, ct);

        foreach (var p in projects)
        {
            decimal primary = p.ContractValue - receipts.GetValueOrDefault(p.Id, 0m);
            decimal served = await outstanding.ClientOutstandingAsync(p.Id, ct);
            if (Math.Abs(primary - served) > Tolerance)
            {
                violations.Add(new IntegrityViolation(
                    "Project", p.Id, p.Name, primary, served,
                    $"contract {p.ContractValue:0.00} − receipts {receipts.GetValueOrDefault(p.Id, 0m):0.00}"));
            }
        }

        return new IntegrityControlResult("Client", formula, violations.Count == 0, violations);
    }

    private async Task<IntegrityControlResult> MultiProjectPaymentAsync(CancellationToken ct)
    {
        const string formula = "Bank Debit = Σ Project Allocations";
        var violations = new List<IntegrityViolation>();

        var settlements = await db.Settlements.AsNoTracking()
            .Where(s => s.Direction == SettlementDirection.Out
                && s.Status == SettlementStatus.Active
                && s.Description == MultiProjectDescription)
            .Select(s => new { s.Id, s.Amount, s.PartyId, s.AccountId })
            .ToListAsync(ct);
        if (settlements.Count == 0)
        {
            return new IntegrityControlResult("Multi-Project Payment", formula, true, violations);
        }

        List<long> ids = settlements.Select(s => s.Id).ToList();

        Dictionary<long, decimal> allocSum = await db.Allocations.AsNoTracking()
            .Where(a => a.SettlementId != null && ids.Contains(a.SettlementId.Value))
            .GroupBy(a => a.SettlementId!.Value)
            .Select(g => new { SettlementId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.SettlementId, x => x.Amount, ct);

        Dictionary<long, decimal> partyLegs = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.SourceType == "VendorPayment" && !e.IsReversal
                && e.PartyId != null && ids.Contains(e.SourceId))
            .GroupBy(e => e.SourceId)
            .Select(g => new { SourceId = g.Key, Net = g.Sum(x => x.Debit - x.Credit) })
            .ToDictionaryAsync(x => x.SourceId, x => x.Net, ct);

        Dictionary<long, decimal> accountLegs = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.SourceType == "VendorPayment" && !e.IsReversal
                && e.AccountId != null && ids.Contains(e.SourceId))
            .GroupBy(e => e.SourceId)
            .Select(g => new { SourceId = g.Key, Amount = g.Sum(x => x.Debit) })
            .ToDictionaryAsync(x => x.SourceId, x => x.Amount, ct);

        Dictionary<long, string> vendorNames = await NamesAsync(
            settlements.Where(s => s.PartyId != null).Select(s => s.PartyId!.Value).Distinct().ToList(), ct);

        foreach (var s in settlements)
        {
            decimal alloc = allocSum.GetValueOrDefault(s.Id, 0m);
            decimal party = partyLegs.GetValueOrDefault(s.Id, 0m);
            bool hasAccount = accountLegs.ContainsKey(s.Id);
            decimal account = accountLegs.GetValueOrDefault(s.Id, 0m);

            bool ok = Math.Abs(alloc - s.Amount) <= Tolerance
                && Math.Abs(party - s.Amount) <= Tolerance
                && (!hasAccount || Math.Abs(account - s.Amount) <= Tolerance);

            if (!ok)
            {
                string name = s.PartyId is { } pid ? vendorNames.GetValueOrDefault(pid, "") : "";
                violations.Add(new IntegrityViolation(
                    "Settlement", s.Id, name, s.Amount, alloc,
                    $"allocations {alloc:0.00}, payable legs {party:0.00}, account leg {account:0.00}"));
            }
        }

        return new IntegrityControlResult("Multi-Project Payment", formula, violations.Count == 0, violations);
    }

    private async Task<Dictionary<long, string>> NamesAsync(List<long> partyIds, CancellationToken ct) =>
        partyIds.Count == 0
            ? []
            : await db.Parties.AsNoTracking()
                .Where(p => partyIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct);
}
