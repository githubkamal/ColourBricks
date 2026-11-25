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
    private const string NumberPrefix = "LW";

    public async Task<WorkEntryDto> RecordWorkAsync(
        RecordWorkRequest request, CancellationToken cancellationToken)
    {
        long? departmentId = await ValidateWorkAsync(request.ProjectId, request.TeamId, cancellationToken);

        string number = await NextNumberAsync(request.Date, cancellationToken);
        Obligation obligation = await PostWorkAsync(
            request.ProjectId, request.TeamId, request.DepartmentId ?? departmentId, request.Date,
            request.AgreedValue, request.WorkType, request.Description, number, cancellationToken);

        return (await GetWorkAsync(obligation.Id, cancellationToken))!;
    }

    public async Task<WorkEntryDto?> UpdateWorkAsync(
        long id, UpdateWorkRequest request, CancellationToken cancellationToken)
    {
        Obligation? old = await db.Obligations
            .FirstOrDefaultAsync(o => o.Id == id && o.Type == ObligationType.SubcontractorWork, cancellationToken);
        if (old is null)
        {
            return null;
        }

        if (old.Status == ObligationStatus.Reversed)
        {
            throw Fail("id", "This work entry has been deleted and can no longer be edited.");
        }

        long? departmentId = await ValidateWorkAsync(request.ProjectId, request.TeamId, cancellationToken);

        List<Settlement> payments = await ActivePaymentsAsync(id, cancellationToken);
        decimal paid = payments.Sum(p => p.Amount);
        if (paid > 0m)
        {
            // A payment's ledger legs carry the project and team, so those can't move.
            if (request.ProjectId != old.ProjectId)
            {
                throw Fail("projectId", "This work entry has payments recorded, so its project can't be changed.");
            }

            if (request.TeamId != old.PartyId)
            {
                throw Fail("teamId", "This work entry has payments recorded, so its team can't be changed.");
            }

            if (request.AgreedValue < paid)
            {
                throw Fail("agreedValue",
                    $"The agreed value can't be less than the {paid:0.00} already paid on this entry.");
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        string number = old.EntryNumber ?? await NextNumberAsync(old.Date, cancellationToken);
        string reason = $"Edited ({number})";

        await ledger.ReverseAsync(WorkSource, old.Id, reason, cancellationToken);
        old.Status = ObligationStatus.Reversed;
        old.EntryNumber = number;
        await db.SaveChangesAsync(cancellationToken);

        Obligation replacement = await PostWorkAsync(
            request.ProjectId, request.TeamId, request.DepartmentId ?? departmentId, request.Date,
            request.AgreedValue, request.WorkType, request.Description, number, cancellationToken);

        // Payments follow the entry, so the paid / outstanding figures stay with it.
        foreach (Settlement payment in payments)
        {
            payment.ObligationId = replacement.Id;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (await GetWorkAsync(replacement.Id, cancellationToken))!;
    }

    public async Task<bool> DeleteWorkAsync(long id, string reason, CancellationToken cancellationToken)
    {
        Obligation? obligation = await db.Obligations
            .FirstOrDefaultAsync(o => o.Id == id && o.Type == ObligationType.SubcontractorWork, cancellationToken);
        if (obligation is null)
        {
            return false;
        }

        if (obligation.Status == ObligationStatus.Reversed)
        {
            throw Fail("id", "This work entry is already deleted.");
        }

        List<Settlement> payments = await ActivePaymentsAsync(id, cancellationToken);
        await RequireNotReconciledAsync(payments.Select(p => p.Id).ToList(), cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Mirrors CustomWorkService.ReverseAsync: reversing the work also reverses its
        // payments, or the books would show cash paid against a payable that's gone.
        await ledger.ReverseAsync(WorkSource, id, reason, cancellationToken);
        foreach (Settlement payment in payments)
        {
            await ledger.ReverseAsync(PaymentSource, payment.Id, reason, cancellationToken);
            payment.Status = SettlementStatus.Reversed;
        }

        obligation.Status = ObligationStatus.Reversed;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReversePaymentAsync(long paymentId, string reason, CancellationToken cancellationToken)
    {
        Settlement? payment = await db.Settlements.FirstOrDefaultAsync(s => s.Id == paymentId, cancellationToken);
        if (payment?.ObligationId is null)
        {
            return false;
        }

        bool isLabour = await db.Obligations.AnyAsync(
            o => o.Id == payment.ObligationId && o.Type == ObligationType.SubcontractorWork, cancellationToken);
        if (!isLabour)
        {
            return false;
        }

        if (payment.Status == SettlementStatus.Reversed)
        {
            throw Fail("id", "This payment is already deleted.");
        }

        await RequireNotReconciledAsync([payment.Id], cancellationToken);

        await ledger.ReverseAsync(PaymentSource, payment.Id, reason, cancellationToken);
        payment.Status = SettlementStatus.Reversed;
        await db.SaveChangesAsync(cancellationToken);
        return true;
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

        if (work.Status == ObligationStatus.Reversed)
        {
            throw Fail("workEntryId", "This work entry has been deleted.");
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

    public async Task<bool> ReverseAsync(long id, string reason, CancellationToken cancellationToken)
    {
        Obligation? obligation = await db.Obligations
            .FirstOrDefaultAsync(o => o.Id == id && o.Type == ObligationType.SubcontractorWork, cancellationToken);
        if (obligation is null)
        {
            return false;
        }

        if (obligation.Status == ObligationStatus.Reversed)
        {
            throw Fail("id", "This work entry is already reversed.");
        }

        decimal paid = await TotalPaidAsync(id, cancellationToken);
        if (paid > 0m)
        {
            throw Fail("id", "This work entry has a payment recorded and cannot be edited.");
        }

        await ledger.ReverseAsync(WorkSource, id, reason, cancellationToken);
        obligation.Status = ObligationStatus.Reversed;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<TeamStatementRowDto>> TeamStatementAsync(
        long teamId, CancellationToken cancellationToken)
    {
        // Deleted (reversed) entries and payments are left out — an edited entry's
        // old version would otherwise show next to its replacement.
        List<Obligation> work = await db.Obligations.AsNoTracking()
            .Where(o => o.Type == ObligationType.SubcontractorWork
                && o.PartyId == teamId
                && o.Status == ObligationStatus.Active)
            .ToListAsync(cancellationToken);

        List<Settlement> payments = await db.Settlements.AsNoTracking()
            .Where(s => s.PartyId == teamId
                && s.Direction == SettlementDirection.Out
                && s.Status == SettlementStatus.Active
                && s.ObligationId != null)
            .ToListAsync(cancellationToken);

        // Only payments against this team's labour work belong on the statement.
        Dictionary<long, Obligation> workById = work.ToDictionary(w => w.Id);
        payments = payments.Where(p => workById.ContainsKey(p.ObligationId!.Value)).ToList();

        List<long> projectIds = work.Select(w => w.ProjectId).Distinct().ToList();
        Dictionary<long, string> projectNames = await db.Projects.AsNoTracking()
            .Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var events =
            work.Select(w => new StatementEvent(
                    w.Date, w.Id, "Work", w.Reference ?? "Work entry", w.Amount, 0m,
                    w.Id, null, w.EntryNumber, projectNames.GetValueOrDefault(w.ProjectId)))
                .Concat(payments.Select(pm =>
                {
                    Obligation w = workById[pm.ObligationId!.Value];
                    return new StatementEvent(
                        pm.Date, pm.Id, "Payment", pm.ReferenceNo ?? "Payment", 0m, pm.Amount,
                        w.Id, pm.Id, w.EntryNumber, projectNames.GetValueOrDefault(w.ProjectId));
                }));

        decimal running = 0m;
        var rows = new List<TeamStatementRowDto>();
        foreach (StatementEvent e in events
                     .OrderBy(e => e.Date).ThenBy(e => e.Kind == "Work" ? 0 : 1).ThenBy(e => e.Id))
        {
            running += e.WorkValue - e.Paid;
            rows.Add(new TeamStatementRowDto(
                e.Date, e.Kind, e.Reference, e.WorkValue, e.Paid, running,
                e.WorkEntryId, e.PaymentId, e.ReferenceNumber, e.ProjectName));
        }

        return rows;
    }

    private sealed record StatementEvent(
        DateOnly Date, long Id, string Kind, string Reference, decimal WorkValue, decimal Paid,
        long WorkEntryId, long? PaymentId, string? ReferenceNumber, string? ProjectName);

    /// <summary>Checks the project and team; returns the team's department.</summary>
    private async Task<long?> ValidateWorkAsync(long projectId, long teamId, CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId, cancellationToken))
        {
            throw Fail("projectId", "The project does not exist.");
        }

        var team = await db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == teamId, cancellationToken);
        if (team is null || (team.Types & Domain.Parties.PartyType.Subcontractor) == 0)
        {
            throw Fail("teamId", "Select a subcontractor team.");
        }

        return team.DepartmentId;
    }

    private async Task<Obligation> PostWorkAsync(
        long projectId, long teamId, long? departmentId, DateOnly date, decimal agreedValue,
        string? workType, string? description, string entryNumber, CancellationToken cancellationToken)
    {
        long labour = await categories.RequireIdAsync("labour", cancellationToken);
        long payable = await categories.RequireIdAsync("subcontractor_payable", cancellationToken);

        var obligation = new Obligation
        {
            Type = ObligationType.SubcontractorWork,
            ProjectId = projectId,
            PartyId = teamId,
            DepartmentId = departmentId,
            Date = date,
            Amount = agreedValue,
            EntryNumber = entryNumber,
            Reference = string.IsNullOrWhiteSpace(workType) ? null : workType.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CategoryId = labour,
            Status = ObligationStatus.Active,
        };
        db.Obligations.Add(obligation);
        await db.SaveChangesAsync(cancellationToken);

        // Work entry posts the expense — no cash.
        await ledger.PostAsync(new LedgerPosting(WorkSource, obligation.Id, date,
        [
            new LedgerLeg(labour, Debit: agreedValue, Credit: 0m, ProjectId: projectId, PartyId: teamId),
            new LedgerLeg(payable, Debit: 0m, Credit: agreedValue, ProjectId: projectId, PartyId: teamId),
        ]), cancellationToken);

        return obligation;
    }

    /// <summary>
    /// Next <c>LW-{year}-{nnnnn}</c> number (client request, 2026-11-21 — a PO-style
    /// reference per entry). Counts distinct numbers so an edited entry, which shares
    /// its number with its reversed original, doesn't use up two.
    /// </summary>
    private async Task<string> NextNumberAsync(DateOnly date, CancellationToken cancellationToken)
    {
        string prefix = $"{NumberPrefix}-{date.Year}-";
        List<string> numbers = await db.Obligations.AsNoTracking()
            .Where(o => o.Type == ObligationType.SubcontractorWork
                && o.EntryNumber != null
                && o.EntryNumber.StartsWith(prefix))
            .Select(o => o.EntryNumber!)
            .Distinct()
            .ToListAsync(cancellationToken);

        int max = numbers
            .Select(n => int.TryParse(n.AsSpan(prefix.Length), out int value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{max + 1:D5}";
    }

    private Task<List<Settlement>> ActivePaymentsAsync(long workEntryId, CancellationToken cancellationToken) =>
        db.Settlements
            .Where(s => s.ObligationId == workEntryId
                && s.Direction == SettlementDirection.Out
                && s.Status == SettlementStatus.Active)
            .ToListAsync(cancellationToken);

    private async Task RequireNotReconciledAsync(IReadOnlyList<long> settlementIds, CancellationToken cancellationToken)
    {
        if (settlementIds.Count == 0)
        {
            return;
        }

        bool linked = await db.ReconciliationLinks.AsNoTracking().AnyAsync(
            l => l.SettlementId != null && settlementIds.Contains(l.SettlementId.Value) && l.UnlinkedAtUtc == null,
            cancellationToken);
        if (linked)
        {
            throw Fail("id",
                "A payment on this entry is linked to a bank transaction. Unlink it from the Reconciliation Queue first.");
        }
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
        var project = await db.Projects.AsNoTracking()
            .Where(p => p.Id == o.ProjectId).Select(p => new { p.Name, p.Code })
            .FirstOrDefaultAsync(cancellationToken);
        decimal paid = await TotalPaidAsync(o.Id, cancellationToken);

        return new WorkEntryDto(
            o.Id, o.ProjectId, o.DepartmentId, o.PartyId!.Value, teamName, o.Date,
            o.Reference, o.Description, o.Amount, paid, o.Amount - paid, o.Status.ToString(),
            o.EntryNumber, project?.Name, project?.Code);
    }

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
