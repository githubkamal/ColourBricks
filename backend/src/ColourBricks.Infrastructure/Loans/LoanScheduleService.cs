using ColourBricks.Application.Loans;
using ColourBricks.Domain.Loans;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Loans;

/// <summary>
/// P7-T02 — persists a loan's amortisation schedule and re-prices its unpaid tail
/// on a rate change (BRD §48, §54). The maths lives in <see cref="EmiAmortisation"/>;
/// this only maps rows to due dates and rows in the database.
/// </summary>
public sealed class LoanScheduleService(AppDbContext db, TimeProvider clock) : ILoanScheduleService
{
    public async Task<IReadOnlyList<LoanEmiInstalmentDto>> GenerateAsync(
        long loanId, GenerateLoanScheduleRequest request, CancellationToken ct)
    {
        Loan loan = await RequireLoanAsync(loanId, ct);

        List<LoanEmiInstalment> existing = await db.Set<LoanEmiInstalment>()
            .Where(i => i.LoanId == loanId).ToListAsync(ct);
        if (existing.Any(i => i.Status != LoanEmiStatus.Pending))
        {
            throw Fail("schedule", "Some instalments are already paid — regenerate instead.");
        }

        db.Set<LoanEmiInstalment>().RemoveRange(existing);

        decimal? clientEmi = request.EmiAmount ?? loan.EmiAmount;
        IReadOnlyList<AmortisationRow> rows = EmiAmortisation.Schedule(
            loan.PrincipalAmount, loan.AnnualInterestRatePercent, loan.TenureMonths, clientEmi);

        var instalments = rows.Select(r => ToEntity(loan, r)).ToList();
        db.Set<LoanEmiInstalment>().AddRange(instalments);

        loan.EmiAmount = rows[0].Emi;
        loan.EmiEndDate = DueDate(loan, rows[^1].InstalmentNo);
        await db.SaveChangesAsync(ct);

        return await GetAsync(loanId, ct);
    }

    public async Task<IReadOnlyList<LoanEmiInstalmentDto>> RegenerateAsync(
        long loanId, RegenerateLoanScheduleRequest request, CancellationToken ct)
    {
        Loan loan = await RequireLoanAsync(loanId, ct);
        if (request.NewAnnualRatePercent < 0m)
        {
            throw Fail("newAnnualRatePercent", "The interest rate cannot be negative.");
        }

        List<LoanEmiInstalment> current = await db.Set<LoanEmiInstalment>()
            .Where(i => i.LoanId == loanId)
            .OrderBy(i => i.InstalmentNo)
            .ToListAsync(ct);
        if (current.Count == 0)
        {
            throw Fail("schedule", "This loan has no schedule to regenerate.");
        }

        int paidCount = current.TakeWhile(i => i.Status == LoanEmiStatus.Paid).Count();
        if (current.Skip(paidCount).Any(i => i.Status != LoanEmiStatus.Pending))
        {
            throw Fail("schedule", "A part-paid instalment blocks regeneration — settle or reverse it first.");
        }

        var currentRows = current
            .Select(i => new AmortisationRow(
                i.InstalmentNo, i.OpeningPrincipal, i.EmiAmount,
                i.PrincipalComponent, i.InterestComponent, i.ClosingPrincipal))
            .ToList();

        IReadOnlyList<AmortisationRow> rebuilt = EmiAmortisation.RegenerateSchedule(
            currentRows, paidCount, request.NewAnnualRatePercent, request.EmiAmount);

        db.Set<LoanEmiInstalment>().RemoveRange(current.Skip(paidCount));
        var tail = rebuilt.Skip(paidCount).Select(r => ToEntity(loan, r)).ToList();
        db.Set<LoanEmiInstalment>().AddRange(tail);

        loan.AnnualInterestRatePercent = request.NewAnnualRatePercent;
        loan.EmiAmount = rebuilt.Count > paidCount ? rebuilt[paidCount].Emi : loan.EmiAmount;
        loan.EmiEndDate = DueDate(loan, rebuilt[^1].InstalmentNo);
        await db.SaveChangesAsync(ct);

        return await GetAsync(loanId, ct);
    }

    public async Task<IReadOnlyList<LoanEmiInstalmentDto>> GetAsync(long loanId, CancellationToken ct)
    {
        List<LoanEmiInstalment> rows = await db.Set<LoanEmiInstalment>().AsNoTracking()
            .Where(i => i.LoanId == loanId)
            .OrderBy(i => i.InstalmentNo)
            .ToListAsync(ct);

        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return rows.Select(i => new LoanEmiInstalmentDto(
            i.Id, i.LoanId, i.InstalmentNo, i.DueDate, i.OpeningPrincipal, i.EmiAmount,
            i.PrincipalComponent, i.InterestComponent, i.ClosingPrincipal,
            i.Status.ToString(), i.PaidAmount, i.PaidDate,
            Overdue: i.Status != LoanEmiStatus.Paid && i.DueDate < today)).ToList();
    }

    public async Task<IReadOnlyList<LoanEmiInstalmentDto>> RebuildPendingTailAsync(long loanId, CancellationToken ct)
    {
        Loan loan = await RequireLoanAsync(loanId, ct);

        List<LoanEmiInstalment> current = await db.Set<LoanEmiInstalment>()
            .Where(i => i.LoanId == loanId)
            .OrderBy(i => i.InstalmentNo)
            .ToListAsync(ct);
        if (current.Count == 0)
        {
            return [];
        }

        int keptCount = current.TakeWhile(i => i.Status != LoanEmiStatus.Pending).Count();
        List<LoanEmiInstalment> pending = current.Skip(keptCount).ToList();
        if (pending.Count == 0)
        {
            return await GetAsync(loanId, ct);
        }

        decimal principalRepaid = await db.Set<LoanEmiPayment>()
            .Where(p => p.LoanId == loanId && p.Status == LoanEmiPaymentStatus.Active)
            .SumAsync(p => (decimal?)p.PrincipalPaid, ct) ?? 0m;
        decimal outstanding = Money.Round(loan.PrincipalAmount - principalRepaid);

        db.Set<LoanEmiInstalment>().RemoveRange(pending);

        if (outstanding > 0m)
        {
            IReadOnlyList<AmortisationRow> tail = EmiAmortisation.Schedule(
                outstanding, loan.AnnualInterestRatePercent, pending.Count);
            var entities = tail
                .Select(r => r with { InstalmentNo = r.InstalmentNo + keptCount })
                .Select(r => ToEntity(loan, r))
                .ToList();
            db.Set<LoanEmiInstalment>().AddRange(entities);
            loan.EmiAmount = entities[0].EmiAmount;
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(loanId, ct);
    }

    private async Task<Loan> RequireLoanAsync(long loanId, CancellationToken ct) =>
        await db.Set<Loan>().FirstOrDefaultAsync(l => l.Id == loanId, ct)
        ?? throw Fail("loanId", "The loan does not exist.");

    private static LoanEmiInstalment ToEntity(Loan loan, AmortisationRow r) => new()
    {
        LoanId = loan.Id,
        InstalmentNo = r.InstalmentNo,
        DueDate = DueDate(loan, r.InstalmentNo),
        OpeningPrincipal = r.OpeningPrincipal,
        EmiAmount = r.Emi,
        PrincipalComponent = r.PrincipalComponent,
        InterestComponent = r.InterestComponent,
        ClosingPrincipal = r.ClosingPrincipal,
        Status = LoanEmiStatus.Pending,
    };

    private static DateOnly DueDate(Loan loan, int instalmentNo) =>
        loan.EmiStartDate.AddMonths(instalmentNo - 1);

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
