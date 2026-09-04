using ColourBricks.Application.Loans;
using ColourBricks.Domain.Loans;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Loans;

/// <summary>
/// P7-T04 — upcoming / overdue EMI detection and the loan outstanding summary
/// (BRD §48, §66). Alerts are idempotent: one persisted row per (instalment, kind).
/// </summary>
public sealed class LoanAlertService(AppDbContext db, TimeProvider clock) : ILoanAlertService
{
    public async Task<LoanAlertsDto> RunAsync(int daysAhead, CancellationToken ct)
    {
        if (daysAhead < 0)
        {
            throw new ValidationException([new ValidationFailure("daysAhead", "The window cannot be negative.")]);
        }

        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        DateOnly windowEnd = today.AddDays(daysAhead);

        var instalments = await (
            from i in db.Set<LoanEmiInstalment>()
            join l in db.Set<Loan>() on i.LoanId equals l.Id
            where l.Status == LoanStatus.Active
            select new { i.Id, i.LoanId, i.InstalmentNo, i.DueDate, i.EmiAmount, i.PaidAmount, i.Status })
            .ToListAsync(ct);

        List<LoanAlert> existing = await db.Set<LoanAlert>().ToListAsync(ct);
        var byKey = existing.ToDictionary(a => (a.InstalmentId, a.Kind));

        foreach (var i in instalments)
        {
            bool unpaid = i.Status != LoanEmiStatus.Paid;
            LoanAlertKind? kind = !unpaid ? null
                : i.DueDate < today ? LoanAlertKind.OverdueEmi
                : i.DueDate <= windowEnd ? LoanAlertKind.UpcomingEmi
                : null;

            // Resolve any alert that no longer applies (instalment paid, or moved out of a bucket).
            foreach (LoanAlertKind k in new[] { LoanAlertKind.UpcomingEmi, LoanAlertKind.OverdueEmi })
            {
                if (byKey.TryGetValue((i.Id, k), out LoanAlert? stale) && stale.ResolvedOn is null && k != kind)
                {
                    stale.ResolvedOn = today;
                }
            }

            if (kind is not { } raise)
            {
                continue;
            }

            decimal due = Money.Round(i.EmiAmount - i.PaidAmount);
            if (byKey.TryGetValue((i.Id, raise), out LoanAlert? row))
            {
                row.Amount = due;
                row.DueDate = i.DueDate;
                row.ResolvedOn = null;
            }
            else
            {
                var created = new LoanAlert
                {
                    LoanId = i.LoanId,
                    InstalmentId = i.Id,
                    Kind = raise,
                    DueDate = i.DueDate,
                    Amount = due,
                    RaisedOn = today,
                };
                db.Set<LoanAlert>().Add(created);
                byKey[(i.Id, raise)] = created;
            }
        }

        await db.SaveChangesAsync(ct);

        var noByInstalment = instalments.ToDictionary(x => x.Id, x => x.InstalmentNo);
        List<LoanAlertDto> open = (await db.Set<LoanAlert>().AsNoTracking()
                .Where(a => a.ResolvedOn == null)
                .OrderBy(a => a.DueDate)
                .ToListAsync(ct))
            .Select(a => new LoanAlertDto(
                a.Id, a.LoanId, a.InstalmentId, noByInstalment.GetValueOrDefault(a.InstalmentId),
                a.Kind.ToString(), a.DueDate, a.Amount, a.DueDate.DayNumber - today.DayNumber))
            .ToList();

        return new LoanAlertsDto(
            daysAhead,
            open.Where(a => a.Kind == nameof(LoanAlertKind.UpcomingEmi)).ToList(),
            open.Where(a => a.Kind == nameof(LoanAlertKind.OverdueEmi)).ToList());
    }

    public async Task<LoanOutstandingSummaryDto> OutstandingSummaryAsync(CancellationToken ct)
    {
        List<Loan> loans = await db.Set<Loan>().AsNoTracking()
            .Where(l => l.Status == LoanStatus.Active)
            .ToListAsync(ct);

        Dictionary<long, decimal> repaidByLoan = (await db.Set<LoanEmiPayment>().AsNoTracking()
                .Where(p => p.Status == LoanEmiPaymentStatus.Active)
                .GroupBy(p => p.LoanId)
                .Select(g => new { LoanId = g.Key, Repaid = g.Sum(x => x.PrincipalPaid) })
                .ToListAsync(ct))
            .ToDictionary(x => x.LoanId, x => x.Repaid);

        List<long> projectIds = loans.Where(l => l.ProjectId is not null).Select(l => l.ProjectId!.Value).Distinct().ToList();
        Dictionary<long, string> projectNames = await db.Projects.AsNoTracking()
            .Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var rows = loans
            .GroupBy(l => l.ProjectId)
            .Select(g => new LoanOutstandingRowDto(
                g.Key,
                g.Key is { } pid ? projectNames.GetValueOrDefault(pid, "") : "(company)",
                g.Count(),
                Money.Round(g.Sum(l => l.PrincipalAmount - repaidByLoan.GetValueOrDefault(l.Id, 0m)))))
            .OrderByDescending(r => r.PrincipalOutstanding)
            .ToList();

        return new LoanOutstandingSummaryDto(Money.Round(rows.Sum(r => r.PrincipalOutstanding)), rows);
    }
}
