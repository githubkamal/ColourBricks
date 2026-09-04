using ColourBricks.Application.Ledger;
using ColourBricks.Application.Loans;
using ColourBricks.Domain.Loans;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Loans;

/// <summary>
/// P7-T05 — the seven BRD §54 loan reports. Principal / interest figures are read
/// back from the ledger so every report reconciles to it; outstanding principal is
/// the disbursement net less principal repaid, so
/// <c>principal paid + outstanding == loan amount</c> for every loan.
/// </summary>
public sealed class LoanReportService(
    AppDbContext db,
    IExpenseCategoryService categories,
    ILoanScheduleService schedule,
    TimeProvider clock) : ILoanReportService
{
    private const string DisbursementSource = "LoanDisbursement";
    private const string PaymentSource = "LoanEmiPayment";

    private sealed record LoanFacts(
        Loan Loan, string LenderName, string ProjectName,
        decimal PrincipalPaid, decimal InterestPaid, decimal Outstanding, DateOnly? NextDueDate);

    public async Task<IReadOnlyList<ProjectWiseLoanRowDto>> ProjectWiseAsync(CancellationToken ct)
    {
        List<LoanFacts> facts = await FactsAsync(null, ct);

        return facts
            .GroupBy(f => (f.Loan.ProjectId, f.ProjectName))
            .Select(g => new ProjectWiseLoanRowDto(
                g.Key.ProjectId, g.Key.ProjectName, g.Count(),
                Money.Round(g.Sum(f => f.Loan.PrincipalAmount)),
                Money.Round(g.Sum(f => f.PrincipalPaid)),
                Money.Round(g.Sum(f => f.Outstanding)),
                Money.Round(g.Sum(f => f.InterestPaid))))
            .OrderByDescending(r => r.OutstandingPrincipal)
            .ToList();
    }

    public async Task<IReadOnlyList<LoanOutstandingReportRowDto>> OutstandingAsync(long? projectId, CancellationToken ct)
    {
        List<LoanFacts> facts = await FactsAsync(projectId, ct);

        return facts
            .Select(f => new LoanOutstandingReportRowDto(
                f.Loan.Id, f.LenderName, f.Loan.ProjectId, f.ProjectName,
                f.Loan.PrincipalAmount, f.PrincipalPaid, f.InterestPaid, f.Outstanding,
                f.NextDueDate, f.Loan.Status.ToString()))
            .OrderByDescending(r => r.OutstandingPrincipal)
            .ToList();
    }

    public Task<IReadOnlyList<LoanEmiInstalmentDto>> ScheduleAsync(long loanId, CancellationToken ct) =>
        schedule.GetAsync(loanId, ct);

    public async Task<IReadOnlyList<EmiPaidRowDto>> EmiPaidAsync(
        long? loanId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        Dictionary<long, string> lenders = await LenderNamesAsync(ct);

        IQueryable<LoanEmiPayment> query = db.Set<LoanEmiPayment>().AsNoTracking()
            .Where(p => p.Status == LoanEmiPaymentStatus.Active);
        if (loanId is { } l) query = query.Where(p => p.LoanId == l);
        if (from is { } f) query = query.Where(p => p.Date >= f);
        if (to is { } t) query = query.Where(p => p.Date <= t);

        List<LoanEmiPayment> rows = await query
            .OrderBy(p => p.Date).ThenBy(p => p.Id).ToListAsync(ct);

        return rows.Select(p => new EmiPaidRowDto(
            p.Id, p.LoanId, lenders.GetValueOrDefault(p.LoanId, ""), p.Date, p.Amount,
            p.PrincipalPaid, p.InterestPaid, p.IsPrepayment, p.Status.ToString())).ToList();
    }

    public async Task<IReadOnlyList<EmiPendingRowDto>> EmiPendingAsync(long? loanId, CancellationToken ct)
    {
        Dictionary<long, string> lenders = await LenderNamesAsync(ct);
        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var rows = await (
            from i in db.Set<LoanEmiInstalment>().AsNoTracking()
            join ln in db.Set<Loan>().AsNoTracking() on i.LoanId equals ln.Id
            where ln.Status == LoanStatus.Active && i.Status != LoanEmiStatus.Paid
                && (loanId == null || i.LoanId == loanId)
            orderby i.DueDate, i.LoanId
            select new { i.LoanId, i.InstalmentNo, i.DueDate, i.EmiAmount, i.PaidAmount }).ToListAsync(ct);

        return rows.Select(r => new EmiPendingRowDto(
            r.LoanId, lenders.GetValueOrDefault(r.LoanId, ""), r.InstalmentNo, r.DueDate,
            r.EmiAmount, Money.Round(r.EmiAmount - r.PaidAmount),
            Math.Max(0, today.DayNumber - r.DueDate.DayNumber))).ToList();
    }

    public async Task<IReadOnlyList<PrincipalVsInterestRowDto>> PrincipalVsInterestAsync(long? loanId, CancellationToken ct)
    {
        List<LoanFacts> facts = await FactsAsync(null, ct);
        return facts
            .Where(f => loanId == null || f.Loan.Id == loanId)
            .Select(f => new PrincipalVsInterestRowDto(
                f.Loan.Id, f.LenderName, f.Loan.PrincipalAmount, f.PrincipalPaid, f.InterestPaid, f.Outstanding))
            .OrderBy(r => r.LoanId)
            .ToList();
    }

    public async Task<IReadOnlyList<DateWiseEmiRowDto>> DateWisePaymentsAsync(
        DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        IQueryable<LoanEmiPayment> query = db.Set<LoanEmiPayment>().AsNoTracking()
            .Where(p => p.Status == LoanEmiPaymentStatus.Active);
        if (from is { } f) query = query.Where(p => p.Date >= f);
        if (to is { } t) query = query.Where(p => p.Date <= t);

        List<LoanEmiPayment> rows = await query
            .Select(p => new LoanEmiPayment
            {
                Date = p.Date, Amount = p.Amount, PrincipalPaid = p.PrincipalPaid, InterestPaid = p.InterestPaid,
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(p => p.Date)
            .Select(g => new DateWiseEmiRowDto(
                g.Key, g.Count(),
                Money.Round(g.Sum(x => x.Amount)),
                Money.Round(g.Sum(x => x.PrincipalPaid)),
                Money.Round(g.Sum(x => x.InterestPaid))))
            .OrderBy(r => r.Date)
            .ToList();
    }

    // ── shared ───────────────────────────────────────────────────────────────

    private async Task<List<LoanFacts>> FactsAsync(long? projectId, CancellationToken ct)
    {
        long emiCost = await categories.RequireIdAsync("loan_emi", ct);
        long payable = await categories.RequireIdAsync("loan_payable", ct);

        List<Loan> loans = await db.Set<Loan>().AsNoTracking()
            .Where(l => projectId == null || l.ProjectId == projectId)
            .ToListAsync(ct);
        var loanIds = loans.Select(l => l.Id).ToList();

        Dictionary<long, string> lenders = await LenderNamesAsync(ct);
        List<long> projectIds = loans.Where(l => l.ProjectId is not null).Select(l => l.ProjectId!.Value).Distinct().ToList();
        Dictionary<long, string> projects = await db.Projects.AsNoTracking()
            .Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        // Active payment ids per loan — reversed payments and their (netted) ledger rows drop out.
        var payments = await db.Set<LoanEmiPayment>().AsNoTracking()
            .Where(p => loanIds.Contains(p.LoanId) && p.Status == LoanEmiPaymentStatus.Active)
            .Select(p => new { p.Id, p.LoanId })
            .ToListAsync(ct);
        var paymentToLoan = payments.ToDictionary(p => p.Id, p => p.LoanId);
        var activePaymentIds = payments.Select(p => p.Id).ToList();

        // Interest expense and principal repayment, straight from the ledger.
        var emiLegs = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.SourceType == PaymentSource && activePaymentIds.Contains(e.SourceId)
                && (e.CategoryId == emiCost || (e.CategoryId == payable && e.AccountId == null && e.PartyId != null)))
            .Select(e => new { e.SourceId, e.CategoryId, Net = e.Debit - e.Credit })
            .ToListAsync(ct);

        var interestByLoan = new Dictionary<long, decimal>();
        var principalPaidByLoan = new Dictionary<long, decimal>();
        foreach (var leg in emiLegs)
        {
            long loan = paymentToLoan[leg.SourceId];
            var bucket = leg.CategoryId == emiCost ? interestByLoan : principalPaidByLoan;
            bucket[loan] = bucket.GetValueOrDefault(loan) + leg.Net;
        }

        // Disbursement net (== loan amount unless the loan was reversed).
        Dictionary<long, decimal> disbursedByLoan = (await db.LedgerEntries.AsNoTracking()
                .Where(e => e.SourceType == DisbursementSource && loanIds.Contains(e.SourceId)
                    && e.CategoryId == payable && e.AccountId == null)
                .Select(e => new { e.SourceId, Net = e.Credit - e.Debit })
                .ToListAsync(ct))
            .GroupBy(x => x.SourceId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Net));

        Dictionary<long, DateOnly> nextDue = (await db.Set<LoanEmiInstalment>().AsNoTracking()
                .Where(i => loanIds.Contains(i.LoanId) && i.Status != LoanEmiStatus.Paid)
                .Select(i => new { i.LoanId, i.DueDate })
                .ToListAsync(ct))
            .GroupBy(x => x.LoanId)
            .ToDictionary(g => g.Key, g => g.Min(x => x.DueDate));

        return loans.Select(l =>
        {
            decimal principalPaid = Money.Round(principalPaidByLoan.GetValueOrDefault(l.Id));
            decimal disbursed = disbursedByLoan.GetValueOrDefault(l.Id, 0m);
            return new LoanFacts(
                l,
                lenders.GetValueOrDefault(l.Id, l.LenderId.ToString()),
                l.ProjectId is { } pid ? projects.GetValueOrDefault(pid, "") : "(company)",
                principalPaid,
                Money.Round(interestByLoan.GetValueOrDefault(l.Id)),
                Money.Round(disbursed - principalPaid),
                nextDue.TryGetValue(l.Id, out DateOnly d) ? d : null);
        }).ToList();
    }

    private async Task<Dictionary<long, string>> LenderNamesAsync(CancellationToken ct)
    {
        var pairs = await (
            from l in db.Set<Loan>().AsNoTracking()
            join p in db.Parties.AsNoTracking() on l.LenderId equals p.Id
            select new { l.Id, p.Name }).ToListAsync(ct);
        return pairs.ToDictionary(x => x.Id, x => x.Name);
    }
}
