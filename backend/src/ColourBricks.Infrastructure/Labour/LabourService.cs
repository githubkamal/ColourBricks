using ColourBricks.Application.Labour;
using ColourBricks.Application.Ledger;
using ColourBricks.Application.Payments;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Labour;

public sealed class LabourService(
    AppDbContext db,
    ILedgerPostingService ledger,
    IExpenseCategoryService categories,
    IPaymentModeService paymentModes) : ILabourService
{
    private const string WorkSource = "SubcontractorWork";
    private const string PaymentSource = "SubcontractorWorkPayment";

    public async Task<WorkEntryDto> RecordWorkAsync(
        RecordWorkRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == request.ProjectId, cancellationToken))
        {
            throw Fail("projectId", "The project does not exist.");
        }

        var team = await db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.TeamId, cancellationToken);
        if (team is null || (team.Types & Domain.Parties.PartyType.Subcontractor) == 0)
        {
            throw Fail("teamId", "Select a subcontractor team.");
        }

        long labour = await categories.RequireIdAsync("labour", cancellationToken);
        long payable = await categories.RequireIdAsync("subcontractor_payable", cancellationToken);

        var obligation = new Obligation
        {
            Type = ObligationType.SubcontractorWork,
            ProjectId = request.ProjectId,
            PartyId = request.TeamId,
            DepartmentId = request.DepartmentId ?? team.DepartmentId,
            Date = request.Date,
            Amount = request.AgreedValue,
            Reference = request.WorkType,
            Description = request.Description,
            CategoryId = labour,
            Status = ObligationStatus.Active,
        };
        db.Obligations.Add(obligation);
        await db.SaveChangesAsync(cancellationToken);

        // Work entry posts the expense — no cash.
        await ledger.PostAsync(new LedgerPosting(WorkSource, obligation.Id, request.Date,
        [
            new LedgerLeg(labour, Debit: request.AgreedValue, Credit: 0m,
                ProjectId: request.ProjectId, PartyId: request.TeamId),
            new LedgerLeg(payable, Debit: 0m, Credit: request.AgreedValue,
                ProjectId: request.ProjectId, PartyId: request.TeamId),
        ]), cancellationToken);

        return (await GetWorkAsync(obligation.Id, cancellationToken))!;
    }

    public async Task<WorkEntryDto?> GetWorkAsync(long id, CancellationToken cancellationToken)
    {
        Obligation? o = await db.Obligations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.Type == ObligationType.SubcontractorWork, cancellationToken);
        return o is null ? null : await ToDtoAsync(o, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkEntryDto>> ListWorkAsync(
        long? projectId, long? teamId, CancellationToken cancellationToken)
    {
        IQueryable<Obligation> query = db.Obligations.AsNoTracking()
            .Where(o => o.Type == ObligationType.SubcontractorWork);

        if (projectId is { } p)
        {
            query = query.Where(o => o.ProjectId == p);
        }

        if (teamId is { } t)
        {
            query = query.Where(o => o.PartyId == t);
        }

        List<Obligation> rows = await query
            .OrderByDescending(o => o.Date).ThenByDescending(o => o.Id)
            .ToListAsync(cancellationToken);

        var dtos = new List<WorkEntryDto>(rows.Count);
        foreach (Obligation row in rows)
        {
            dtos.Add(await ToDtoAsync(row, cancellationToken));
        }

        return dtos;
    }

    public async Task<WorkPaymentDto> PayAsync(
        long workEntryId, PayWorkRequest request, CancellationToken cancellationToken)
    {
        Obligation? work = await db.Obligations
            .FirstOrDefaultAsync(o => o.Id == workEntryId && o.Type == ObligationType.SubcontractorWork,
                cancellationToken);
        if (work is null)
        {
            throw Fail("workEntryId", "The work entry does not exist.");
        }

        decimal paid = await TotalPaidAsync(workEntryId, cancellationToken);
        if (request.Amount > work.Amount - paid)
        {
            throw Fail("amount",
                $"Payment of {request.Amount:0.00} exceeds the remaining {work.Amount - paid:0.00} on this work entry.");
        }

        await paymentModes.ValidateInstructionAsync(
            new PaymentInstruction(request.PaymentModeId, request.ReferenceNo, request.AccountId),
            cancellationToken);

        long payable = await categories.RequireIdAsync("subcontractor_payable", cancellationToken);

        var settlement = new Settlement
        {
            Direction = SettlementDirection.Out,
            ProjectId = work.ProjectId,
            PartyId = work.PartyId,
            ObligationId = workEntryId,
            Date = request.Date,
            Amount = request.Amount,
            Frequency = request.Frequency,
            PaymentModeId = request.PaymentModeId,
            AccountId = request.AccountId,
            ReferenceNo = request.ReferenceNo,
            Description = "Labour payment",
            Status = SettlementStatus.Active,
        };
        db.Settlements.Add(settlement);
        await db.SaveChangesAsync(cancellationToken);

        // Payment moves cash and settles the payable — it never posts an expense.
        var legs = new List<LedgerLeg>
        {
            new(payable, Debit: request.Amount, Credit: 0m,
                ProjectId: work.ProjectId, PartyId: work.PartyId),
        };
        if (request.AccountId is { } accountId)
        {
            legs.Add(new LedgerLeg(payable, Debit: request.Amount, Credit: 0m, AccountId: accountId));
        }

        await ledger.PostAsync(
            new LedgerPosting(PaymentSource, settlement.Id, request.Date, legs), cancellationToken);

        return new WorkPaymentDto(
            settlement.Id, workEntryId, settlement.Date, settlement.Amount,
            settlement.Frequency ?? PaymentFrequency.AdHoc, settlement.PaymentModeId,
            settlement.AccountId, settlement.ReferenceNo);
    }

    public async Task<IReadOnlyList<TeamStatementRowDto>> TeamStatementAsync(
        long teamId, CancellationToken cancellationToken)
    {
        List<Obligation> work = await db.Obligations.AsNoTracking()
            .Where(o => o.Type == ObligationType.SubcontractorWork && o.PartyId == teamId)
            .ToListAsync(cancellationToken);

        List<Settlement> payments = await db.Settlements.AsNoTracking()
            .Where(s => s.PartyId == teamId
                && s.Direction == SettlementDirection.Out
                && s.Status == SettlementStatus.Active
                && s.ObligationId != null)
            .ToListAsync(cancellationToken);

        IEnumerable<(DateOnly Date, long Id, string Kind, string Reference, decimal WorkValue, decimal Paid)> events =
            work.Select(w => (w.Date, w.Id, "Work", w.Reference ?? "Work entry", w.Amount, 0m))
                .Concat(payments.Select(pm =>
                    (pm.Date, pm.Id, "Payment", pm.ReferenceNo ?? "Payment", 0m, pm.Amount)));

        decimal running = 0m;
        var rows = new List<TeamStatementRowDto>();
        foreach (var e in events.OrderBy(e => e.Date).ThenBy(e => e.Kind == "Work" ? 0 : 1).ThenBy(e => e.Id))
        {
            running += e.WorkValue - e.Paid;
            rows.Add(new TeamStatementRowDto(e.Date, e.Kind, e.Reference, e.WorkValue, e.Paid, running));
        }

        return rows;
    }

    private async Task<decimal> TotalPaidAsync(long workEntryId, CancellationToken cancellationToken) =>
        await db.Settlements.AsNoTracking()
            .Where(s => s.ObligationId == workEntryId
                && s.Direction == SettlementDirection.Out
                && s.Status == SettlementStatus.Active)
            .SumAsync(s => (decimal?)s.Amount, cancellationToken) ?? 0m;

    private async Task<WorkEntryDto> ToDtoAsync(Obligation o, CancellationToken cancellationToken)
    {
        string teamName = await db.Parties.AsNoTracking()
            .Where(p => p.Id == o.PartyId).Select(p => p.Name).FirstOrDefaultAsync(cancellationToken) ?? "";
        decimal paid = await TotalPaidAsync(o.Id, cancellationToken);

        return new WorkEntryDto(
            o.Id, o.ProjectId, o.DepartmentId, o.PartyId!.Value, teamName, o.Date,
            o.Reference, o.Description, o.Amount, paid, o.Amount - paid, o.Status.ToString());
    }

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
