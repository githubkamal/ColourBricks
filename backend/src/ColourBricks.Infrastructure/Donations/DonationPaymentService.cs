using ColourBricks.Application.Donations;
using ColourBricks.Application.Ledger;
using ColourBricks.Application.Payments;
using ColourBricks.Domain.Donations;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Donations;

public sealed class DonationPaymentService(
    AppDbContext db,
    ILedgerPostingService ledger,
    IExpenseCategoryService categories,
    IPaymentModeService paymentModes) : IDonationPaymentService
{
    private const string PaymentSource = "TempleDonationPayment";
    private const string PaymentDescription = "Temple donation payment";

    public async Task<DonationPaymentDto> PayAsync(
        long projectId, long templeId, PayDonationRequest request, CancellationToken cancellationToken)
    {
        ProjectDonation? donation = await db.ProjectDonations.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ProjectId == projectId, cancellationToken);
        if (donation is null)
        {
            throw Fail("projectId", "This project has no temple donation set up.");
        }

        ProjectDonationTemple? split = await db.ProjectDonationTemples.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProjectDonationId == donation.Id && s.TempleId == templeId, cancellationToken);
        if (split is null)
        {
            throw Fail("templeId", "This temple has no allocation in the project's donation.");
        }

        decimal paid = await PaidForTempleAsync(projectId, templeId, cancellationToken);
        if (request.Amount > split.Amount - paid)
        {
            throw Fail("amount",
                $"Payment of {request.Amount:0.00} exceeds the temple's remaining {split.Amount - paid:0.00}.");
        }

        await paymentModes.ValidateInstructionAsync(
            new PaymentInstruction(request.PaymentModeId, request.ReferenceNo, request.AccountId),
            cancellationToken);

        long obligationId = await db.Obligations.AsNoTracking()
            .Where(o => o.Type == ObligationType.TempleDonation && o.ProjectId == projectId)
            .Select(o => o.Id)
            .FirstAsync(cancellationToken);

        long payableCategory = await categories.RequireIdAsync("temple_donation_payable", cancellationToken);

        var settlement = new Settlement
        {
            Direction = SettlementDirection.Out,
            ProjectId = projectId,
            PartyId = templeId,
            ObligationId = obligationId,
            Date = request.Date,
            Amount = request.Amount,
            PaymentModeId = request.PaymentModeId,
            AccountId = request.AccountId,
            ReferenceNo = request.ReferenceNo,
            Description = PaymentDescription,
            Status = SettlementStatus.Active,
        };
        db.Settlements.Add(settlement);
        await db.SaveChangesAsync(cancellationToken);

        // Settles the allocation — no cost leg, so the donation expense is never double-counted.
        var legs = new List<LedgerLeg>
        {
            new(payableCategory, Debit: request.Amount, Credit: 0m, ProjectId: projectId, PartyId: templeId),
        };
        if (request.AccountId is { } accountId)
        {
            legs.Add(new LedgerLeg(payableCategory, Debit: request.Amount, Credit: 0m, AccountId: accountId));
        }

        await ledger.PostAsync(
            new LedgerPosting(PaymentSource, settlement.Id, request.Date, legs), cancellationToken);

        return new DonationPaymentDto(
            settlement.Id, projectId, templeId, settlement.Date, settlement.Amount,
            settlement.PaymentModeId, settlement.AccountId, settlement.ReferenceNo);
    }

    public async Task<IReadOnlyList<DonationTempleOutstandingDto>> OutstandingByTempleAsync(
        long projectId, CancellationToken cancellationToken)
    {
        ProjectDonation? donation = await db.ProjectDonations.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ProjectId == projectId, cancellationToken);
        if (donation is null)
        {
            return [];
        }

        List<ProjectDonationTemple> splits = await db.ProjectDonationTemples.AsNoTracking()
            .Where(s => s.ProjectDonationId == donation.Id)
            .ToListAsync(cancellationToken);

        List<long> templeIds = splits.Select(s => s.TempleId).ToList();
        Dictionary<long, string> names = await db.Parties.AsNoTracking()
            .Where(p => templeIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var result = new List<DonationTempleOutstandingDto>(splits.Count);
        foreach (ProjectDonationTemple split in splits)
        {
            decimal paid = await PaidForTempleAsync(projectId, split.TempleId, cancellationToken);
            result.Add(new DonationTempleOutstandingDto(
                split.TempleId, names.GetValueOrDefault(split.TempleId, ""),
                split.Amount, paid, split.Amount - paid));
        }

        return result;
    }

    private async Task<decimal> PaidForTempleAsync(
        long projectId, long templeId, CancellationToken cancellationToken) =>
        await db.Settlements.AsNoTracking()
            .Where(s => s.ProjectId == projectId
                && s.PartyId == templeId
                && s.Direction == SettlementDirection.Out
                && s.Status == SettlementStatus.Active
                && s.Description == PaymentDescription)
            .SumAsync(s => (decimal?)s.Amount, cancellationToken) ?? 0m;

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
