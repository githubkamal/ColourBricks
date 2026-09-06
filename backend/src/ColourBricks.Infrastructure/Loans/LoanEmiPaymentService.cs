using ColourBricks.Application.Ledger;
using ColourBricks.Application.Loans;
using ColourBricks.Application.Payments;
using ColourBricks.Domain.Loans;
using ColourBricks.Domain.Services;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Loans;

/// <summary>
/// P7-T03 — EMI payments (BRD §48). Interest is a cost on <c>loan_emi</c> (the
/// loan's project, or company-level when it has none); principal only debits
/// <c>loan_payable</c> and never counts as an expense. Prepayments are all
/// principal and re-amortise the pending tail.
/// </summary>
public sealed class LoanEmiPaymentService(
    AppDbContext db,
    ILedgerPostingService ledger,
    IExpenseCategoryService categories,
    IPaymentModeService paymentModes,
    ILoanScheduleService schedule) : ILoanEmiPaymentService
{
    private const string SourceType = "LoanEmiPayment";

    public async Task<LoanEmiPaymentDto> PayAsync(long loanId, PayEmiRequest request, CancellationToken ct)
    {
        Loan loan = await RequireActiveLoanAsync(loanId, ct);

        LoanEmiInstalment instalment = await db.Set<LoanEmiInstalment>()
            .FirstOrDefaultAsync(i => i.Id == request.InstalmentId && i.LoanId == loanId, ct)
            ?? throw Fail("instalmentId", "That instalment is not on this loan.");
        if (instalment.Status == LoanEmiStatus.Paid)
        {
            throw Fail("instalmentId", "This instalment is already paid.");
        }

        decimal due = Money.Round(instalment.EmiAmount - instalment.PaidAmount);
        decimal amount = Money.Round(request.Amount ?? due);
        if (amount <= 0m)
        {
            throw Fail("amount", "The payment must be greater than zero.");
        }

        if (amount > due)
        {
            throw Fail("amount", $"The payment exceeds the ₹{due:0.00} still due on this instalment.");
        }

        // Apply to interest first, then principal (BRD §48).
        decimal interestBefore = Math.Min(instalment.PaidAmount, instalment.InterestComponent);
        decimal interestAfter = Math.Min(instalment.PaidAmount + amount, instalment.InterestComponent);
        decimal interestPaid = Money.Round(interestAfter - interestBefore);
        decimal principalPaid = Money.Round(amount - interestPaid);

        await paymentModes.ValidateInstructionAsync(
            new PaymentInstruction(request.PaymentModeId, request.ReferenceNo, request.AccountId), ct);

        var payment = new LoanEmiPayment
        {
            LoanId = loanId,
            InstalmentId = instalment.Id,
            Date = request.Date,
            Amount = amount,
            PrincipalPaid = principalPaid,
            InterestPaid = interestPaid,
            PaymentModeId = request.PaymentModeId,
            AccountId = request.AccountId,
            ReferenceNo = request.ReferenceNo?.Trim(),
            IsPrepayment = false,
            Status = LoanEmiPaymentStatus.Active,
        };
        db.Set<LoanEmiPayment>().Add(payment);
        Settlement? anchor = AddReconciliationAnchor(loan, payment);

        instalment.PaidAmount = Money.Round(instalment.PaidAmount + amount);
        instalment.Status = instalment.PaidAmount >= instalment.EmiAmount
            ? LoanEmiStatus.Paid
            : LoanEmiStatus.PartPaid;
        instalment.PaidDate = instalment.Status == LoanEmiStatus.Paid ? request.Date : instalment.PaidDate;
        await db.SaveChangesAsync(ct);

        await PostAsync(loan, payment, ct);
        return ToDto(payment, anchor?.Id);
    }

    public async Task<LoanEmiPaymentDto> PrepayAsync(long loanId, PrepayLoanRequest request, CancellationToken ct)
    {
        Loan loan = await RequireActiveLoanAsync(loanId, ct);

        decimal amount = Money.Round(request.Amount);
        if (amount <= 0m)
        {
            throw Fail("amount", "The prepayment must be greater than zero.");
        }

        decimal outstanding = await OutstandingPrincipalAsync(loanId, loan.PrincipalAmount, ct);
        if (amount > outstanding)
        {
            throw Fail("amount", $"The prepayment exceeds the ₹{outstanding:0.00} principal outstanding.");
        }

        await paymentModes.ValidateInstructionAsync(
            new PaymentInstruction(request.PaymentModeId, request.ReferenceNo, request.AccountId), ct);

        var payment = new LoanEmiPayment
        {
            LoanId = loanId,
            InstalmentId = null,
            Date = request.Date,
            Amount = amount,
            PrincipalPaid = amount,
            InterestPaid = 0m,
            PaymentModeId = request.PaymentModeId,
            AccountId = request.AccountId,
            ReferenceNo = request.ReferenceNo?.Trim(),
            IsPrepayment = true,
            Status = LoanEmiPaymentStatus.Active,
        };
        db.Set<LoanEmiPayment>().Add(payment);
        Settlement? anchor = AddReconciliationAnchor(loan, payment);
        await db.SaveChangesAsync(ct);

        await PostAsync(loan, payment, ct);

        // The lower balance re-amortises the still-pending instalments (BRD §48).
        await schedule.RebuildPendingTailAsync(loanId, ct);
        return ToDto(payment, anchor?.Id);
    }

    public async Task<IReadOnlyList<LoanEmiPaymentDto>> ListAsync(long loanId, CancellationToken ct)
    {
        List<LoanEmiPayment> rows = await db.Set<LoanEmiPayment>().AsNoTracking()
            .Where(p => p.LoanId == loanId)
            .OrderByDescending(p => p.Date).ThenByDescending(p => p.Id)
            .ToListAsync(ct);
        return rows.Select(p => ToDto(p)).ToList();
    }

    public async Task<bool> ReverseAsync(long paymentId, string reason, CancellationToken ct)
    {
        LoanEmiPayment? payment = await db.Set<LoanEmiPayment>().FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null)
        {
            return false;
        }

        if (payment.Status == LoanEmiPaymentStatus.Reversed)
        {
            throw Fail("id", "This payment is already reversed.");
        }

        await ledger.ReverseAsync(SourceType, paymentId, reason, ct);
        payment.Status = LoanEmiPaymentStatus.Reversed;

        if (payment.InstalmentId is { } instalmentId)
        {
            LoanEmiInstalment? instalment = await db.Set<LoanEmiInstalment>()
                .FirstOrDefaultAsync(i => i.Id == instalmentId, ct);
            if (instalment is not null)
            {
                instalment.PaidAmount = Money.Round(Math.Max(0m, instalment.PaidAmount - payment.Amount));
                instalment.Status = instalment.PaidAmount <= 0m
                    ? LoanEmiStatus.Pending
                    : LoanEmiStatus.PartPaid;
                instalment.PaidDate = instalment.Status == LoanEmiStatus.Paid ? instalment.PaidDate : null;
            }
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task PostAsync(Loan loan, LoanEmiPayment payment, CancellationToken ct)
    {
        long emiCost = await categories.RequireIdAsync("loan_emi", ct);
        long payable = await categories.RequireIdAsync("loan_payable", ct);

        var legs = new List<LedgerLeg>();
        if (payment.InterestPaid > 0m)
        {
            // Interest is a cost — the loan's project, or company-level when it has none.
            legs.Add(new LedgerLeg(emiCost, Debit: payment.InterestPaid, Credit: 0m, ProjectId: loan.ProjectId));
        }

        if (payment.PrincipalPaid > 0m)
        {
            // Principal only lowers the liability; it is never an expense.
            legs.Add(new LedgerLeg(payable, Debit: payment.PrincipalPaid, Credit: 0m, PartyId: loan.LenderId));
        }

        if (payment.AccountId is { } accountId)
        {
            // Cash out of the bank account.
            legs.Add(new LedgerLeg(payable, Debit: payment.Amount, Credit: 0m, AccountId: accountId));
        }

        await ledger.PostAsync(new LedgerPosting(SourceType, payment.Id, payment.Date, legs), ct);
    }

    private async Task<decimal> OutstandingPrincipalAsync(long loanId, decimal principal, CancellationToken ct)
    {
        decimal repaid = await db.Set<LoanEmiPayment>()
            .Where(p => p.LoanId == loanId && p.Status == LoanEmiPaymentStatus.Active)
            .SumAsync(p => (decimal?)p.PrincipalPaid, ct) ?? 0m;
        return Money.Round(principal - repaid);
    }

    private async Task<Loan> RequireActiveLoanAsync(long loanId, CancellationToken ct)
    {
        Loan loan = await db.Set<Loan>().FirstOrDefaultAsync(l => l.Id == loanId, ct)
            ?? throw Fail("loanId", "The loan does not exist.");
        if (loan.Status != LoanStatus.Active)
        {
            throw Fail("loanId", "This loan is not active.");
        }

        return loan;
    }

    /// <summary>
    /// A Settlement row with no ledger legs of its own — purely a reconciliation
    /// anchor so this payment can optionally be linked to a bank transaction via
    /// the existing <c>reconcile-debit</c> flow (client request, 2026-09-06). Only
    /// created when the payment actually moved money through an account; the real
    /// ledger posting still runs through <see cref="PostAsync"/> keyed off the
    /// <see cref="LoanEmiPayment"/> itself, unchanged.
    /// </summary>
    private Settlement? AddReconciliationAnchor(Loan loan, LoanEmiPayment payment)
    {
        if (payment.AccountId is not { } accountId)
        {
            return null;
        }

        var anchor = new Settlement
        {
            Direction = SettlementDirection.Out,
            ProjectId = loan.ProjectId,
            PartyId = loan.LenderId,
            Date = payment.Date,
            Amount = payment.Amount,
            PaymentModeId = payment.PaymentModeId,
            AccountId = accountId,
            ReferenceNo = payment.ReferenceNo,
            Description = "Loan EMI payment (reconciliation anchor)",
            Status = SettlementStatus.Active,
        };
        db.Settlements.Add(anchor);
        return anchor;
    }

    private static LoanEmiPaymentDto ToDto(LoanEmiPayment p, long? settlementId = null) => new(
        p.Id, p.LoanId, p.InstalmentId, p.Date, p.Amount, p.PrincipalPaid, p.InterestPaid,
        p.PaymentModeId, p.AccountId, p.ReferenceNo, p.IsPrepayment, p.Status.ToString(), settlementId);

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
