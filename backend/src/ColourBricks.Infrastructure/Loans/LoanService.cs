using ColourBricks.Application.Ledger;
using ColourBricks.Application.Loans;
using ColourBricks.Domain.Loans;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Loans;

/// <summary>
/// P7-T01 — the loan master and its disbursement (BRD §48). Recording a loan
/// credits the bank account and raises a liability at the lender; it never touches
/// project income or cost. Outstanding principal is derived from the ledger, not
/// stored.
/// </summary>
public sealed class LoanService(
    AppDbContext db,
    ILedgerPostingService ledger,
    IExpenseCategoryService categories) : ILoanService
{
    private const string DisbursementSource = "LoanDisbursement";
    private const string PayableSlug = "loan_payable";

    public async Task<LoanDto> RecordAsync(RecordLoanRequest request, CancellationToken ct)
    {
        Party? lender = await db.Parties.FirstOrDefaultAsync(p => p.Id == request.LenderId, ct);
        if (lender is null)
        {
            throw Fail("lenderId", "The lender does not exist.");
        }

        if (!lender.Types.HasFlag(PartyType.Lender))
        {
            throw Fail("lenderId", "That party is not a lender.");
        }

        if (request.ProjectId is { } projectId
            && !await db.Projects.AnyAsync(p => p.Id == projectId, ct))
        {
            throw Fail("projectId", "The project does not exist.");
        }

        if (!await db.Accounts.AnyAsync(a => a.Id == request.DisbursementAccountId, ct))
        {
            throw Fail("disbursementAccountId", "The disbursement account does not exist.");
        }

        decimal principal = Money.Round(request.PrincipalAmount);
        if (principal <= 0m)
        {
            throw Fail("principalAmount", "The principal must be greater than zero.");
        }

        if (request.AnnualInterestRatePercent < 0m)
        {
            throw Fail("annualInterestRatePercent", "The interest rate cannot be negative.");
        }

        if (request.TenureMonths <= 0)
        {
            throw Fail("tenureMonths", "The tenure must be at least one month.");
        }

        if (request.EmiAmount is { } emi && emi <= 0m)
        {
            throw Fail("emiAmount", "The EMI amount must be greater than zero.");
        }

        if (request.EmiStartDate < request.StartDate)
        {
            throw Fail("emiStartDate", "The first EMI cannot fall before the loan start date.");
        }

        var loan = new Loan
        {
            ProjectId = request.ProjectId,
            LenderId = request.LenderId,
            PrincipalAmount = principal,
            AnnualInterestRatePercent = request.AnnualInterestRatePercent,
            StartDate = request.StartDate,
            TenureMonths = request.TenureMonths,
            EmiAmount = request.EmiAmount is { } e ? Money.Round(e) : null,
            EmiStartDate = request.EmiStartDate,
            EmiEndDate = null,
            DisbursementAccountId = request.DisbursementAccountId,
            DisbursementDate = request.DisbursementDate,
            Reference = request.Reference?.Trim(),
            Notes = request.Notes?.Trim(),
            Status = LoanStatus.Active,
        };
        db.Set<Loan>().Add(loan);
        await db.SaveChangesAsync(ct);

        long payable = await categories.RequireIdAsync(PayableSlug, ct);

        // Disbursement: cash into the bank account, liability up at the lender.
        // Both are credits — neither leg is a cost or income category, so no project
        // report moves (plan.md §5.3; BRD §48).
        await ledger.PostAsync(new LedgerPosting(DisbursementSource, loan.Id, request.DisbursementDate,
        [
            new LedgerLeg(payable, Debit: 0m, Credit: principal, AccountId: request.DisbursementAccountId),
            new LedgerLeg(payable, Debit: 0m, Credit: principal, PartyId: request.LenderId),
        ]), ct);

        return await GetAsync(loan.Id, ct) ?? throw new InvalidOperationException();
    }

    public async Task<IReadOnlyList<LoanDto>> ListAsync(long? projectId, long? lenderId, CancellationToken ct)
    {
        IQueryable<Loan> query = db.Set<Loan>().AsNoTracking();
        if (projectId is { } p)
        {
            query = query.Where(l => l.ProjectId == p);
        }

        if (lenderId is { } le)
        {
            query = query.Where(l => l.LenderId == le);
        }

        List<Loan> rows = await query.OrderByDescending(l => l.Id).ToListAsync(ct);

        var dtos = new List<LoanDto>(rows.Count);
        foreach (Loan row in rows)
        {
            dtos.Add(await ToDtoAsync(row, ct));
        }

        return dtos;
    }

    public async Task<LoanDto?> GetAsync(long id, CancellationToken ct)
    {
        Loan? loan = await db.Set<Loan>().AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);
        return loan is null ? null : await ToDtoAsync(loan, ct);
    }

    public async Task<bool> ReverseAsync(long id, string reason, CancellationToken ct)
    {
        Loan? loan = await db.Set<Loan>().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (loan is null)
        {
            return false;
        }

        if (loan.Status == LoanStatus.Reversed)
        {
            throw Fail("id", "This loan is already reversed.");
        }

        await ledger.ReverseAsync(DisbursementSource, id, reason, ct);
        loan.Status = LoanStatus.Reversed;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<LoanDto> ToDtoAsync(Loan loan, CancellationToken ct)
    {
        string lenderName = await db.Parties.AsNoTracking()
            .Where(p => p.Id == loan.LenderId).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? "";

        decimal outstanding = await OutstandingPrincipalAsync(loan, ct);

        return new LoanDto(
            loan.Id, loan.ProjectId, loan.LenderId, lenderName,
            loan.PrincipalAmount, loan.AnnualInterestRatePercent,
            loan.StartDate, loan.TenureMonths, loan.EmiAmount,
            loan.EmiStartDate, loan.EmiEndDate,
            loan.DisbursementAccountId, loan.DisbursementDate,
            loan.Reference, loan.Notes, loan.Status.ToString(),
            outstanding);
    }

    /// <summary>
    /// Derived outstanding principal = the loan-payable balance traceable to this
    /// loan's disbursement (Σ credit − debit, which nets to zero once reversed)
    /// minus principal repaid. P7-T03 supplies the repaid component from the EMI
    /// schedule; until then only the disbursement exists.
    /// </summary>
    private async Task<decimal> OutstandingPrincipalAsync(Loan loan, CancellationToken ct)
    {
        long payable = await categories.RequireIdAsync(PayableSlug, ct);

        decimal disbursed = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.CategoryId == payable
                && e.SourceType == DisbursementSource
                && e.SourceId == loan.Id
                && e.AccountId == null)
            .SumAsync(e => (decimal?)(e.Credit - e.Debit), ct) ?? 0m;

        decimal principalRepaid = await PrincipalRepaidAsync(loan.Id, ct);
        return Money.Round(disbursed - principalRepaid);
    }

    /// <summary>Σ principal component of this loan's active EMI payments and prepayments (P7-T03).</summary>
    private async Task<decimal> PrincipalRepaidAsync(long loanId, CancellationToken ct) =>
        await db.Set<LoanEmiPayment>()
            .Where(p => p.LoanId == loanId && p.Status == LoanEmiPaymentStatus.Active)
            .SumAsync(p => (decimal?)p.PrincipalPaid, ct) ?? 0m;

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
