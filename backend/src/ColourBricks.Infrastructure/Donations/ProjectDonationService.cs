using ColourBricks.Application.Donations;
using ColourBricks.Application.Ledger;
using ColourBricks.Domain.Donations;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Projects;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Donations;

public sealed class ProjectDonationService(
    AppDbContext db,
    ILedgerPostingService ledger,
    IExpenseCategoryService categories) : IProjectDonationService
{
    internal const string ObligationSource = "TempleDonation";
    public async Task<ProjectDonationDto?> GetForProjectAsync(
        long projectId, CancellationToken cancellationToken)
    {
        ProjectDonation? donation = await db.ProjectDonations.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ProjectId == projectId, cancellationToken);
        return donation is null ? null : await ToDtoAsync(donation, cancellationToken);
    }

    public async Task<ProjectDonationDto> UpsertAsync(
        long projectId, UpsertProjectDonationRequest request, CancellationToken cancellationToken)
    {
        Project project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw Fail("projectId", "The project does not exist.");

        ValidateBasis(request);

        decimal amount = DonationCalculator.Compute(
            request.Basis, request.Percentage, request.FixedAmount, project.ContractValue);

        await ValidateSplitAsync(request, amount, cancellationToken);

        ProjectDonation? donation = await db.ProjectDonations
            .FirstOrDefaultAsync(d => d.ProjectId == projectId, cancellationToken);

        if (donation is null)
        {
            donation = new ProjectDonation { ProjectId = projectId };
            db.ProjectDonations.Add(donation);
        }

        donation.Basis = request.Basis;
        donation.Percentage = request.Basis == DonationBasis.Percentage ? request.Percentage : null;
        donation.FixedAmount = request.Basis == DonationBasis.Fixed ? request.FixedAmount : null;
        donation.ContractValueSnapshot = project.ContractValue;
        donation.DonationAmount = amount;
        donation.NeedsReview = donation.PaidAmount > amount;

        await db.SaveChangesAsync(cancellationToken);

        List<ProjectDonationTemple> existing = await db.ProjectDonationTemples
            .Where(s => s.ProjectDonationId == donation.Id)
            .ToListAsync(cancellationToken);
        db.ProjectDonationTemples.RemoveRange(existing);

        foreach (DonationTempleSplitInput split in request.Temples)
        {
            db.ProjectDonationTemples.Add(new ProjectDonationTemple
            {
                ProjectDonationId = donation.Id,
                TempleId = split.TempleId,
                Amount = split.Amount,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        await EnsureObligationAsync(donation, request.Temples, cancellationToken);

        return await ToDtoAsync(donation, cancellationToken);
    }

    /// <summary>
    /// Creates the project's temple-donation obligation and its ledger posting the
    /// first time the split is saved (BRD §26 — allocation creates the obligation,
    /// the payment settles it). Later split revisions only update the header amount;
    /// re-posting a revised allocation is a follow-up task.
    /// </summary>
    private async Task EnsureObligationAsync(
        ProjectDonation donation,
        IReadOnlyList<DonationTempleSplitInput> temples,
        CancellationToken cancellationToken)
    {
        Obligation? obligation = await db.Obligations.FirstOrDefaultAsync(
            o => o.Type == ObligationType.TempleDonation && o.ProjectId == donation.ProjectId,
            cancellationToken);

        if (obligation is not null)
        {
            obligation.Amount = donation.DonationAmount;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        long donationCategory = await categories.RequireIdAsync("temple_donation", cancellationToken);
        long payableCategory = await categories.RequireIdAsync("temple_donation_payable", cancellationToken);

        obligation = new Obligation
        {
            Type = ObligationType.TempleDonation,
            ProjectId = donation.ProjectId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Amount = donation.DonationAmount,
            CategoryId = donationCategory,
            Description = "Temple donation allocation",
            Status = ObligationStatus.Active,
        };
        db.Obligations.Add(obligation);
        await db.SaveChangesAsync(cancellationToken);

        var legs = new List<LedgerLeg>();
        foreach (DonationTempleSplitInput split in temples)
        {
            legs.Add(new LedgerLeg(donationCategory, Debit: split.Amount, Credit: 0m,
                ProjectId: donation.ProjectId, PartyId: split.TempleId));
            legs.Add(new LedgerLeg(payableCategory, Debit: 0m, Credit: split.Amount,
                ProjectId: donation.ProjectId, PartyId: split.TempleId));
        }

        await ledger.PostAsync(
            new LedgerPosting(ObligationSource, obligation.Id, obligation.Date, legs), cancellationToken);
    }

    public async Task RecomputeForContractValueAsync(
        long projectId, decimal newContractValue, CancellationToken cancellationToken)
    {
        ProjectDonation? donation = await db.ProjectDonations
            .FirstOrDefaultAsync(d => d.ProjectId == projectId, cancellationToken);

        if (donation is null || donation.Basis != DonationBasis.Percentage)
        {
            return;
        }

        decimal newAmount = DonationCalculator.Compute(
            DonationBasis.Percentage, donation.Percentage, fixedAmount: null, newContractValue);

        decimal splitTotal = await db.ProjectDonationTemples
            .Where(s => s.ProjectDonationId == donation.Id)
            .SumAsync(s => (decimal?)s.Amount, cancellationToken) ?? 0m;

        donation.ContractValueSnapshot = newContractValue;
        donation.DonationAmount = newAmount;
        donation.NeedsReview = splitTotal != newAmount || donation.PaidAmount > newAmount;

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateBasis(UpsertProjectDonationRequest request)
    {
        if (request.Basis == DonationBasis.Percentage
            && request.Percentage is not (> 0m and <= 100m))
        {
            throw Fail("percentage", "Percentage must be between 0 and 100.");
        }

        if (request.Basis == DonationBasis.Fixed && request.FixedAmount is not (>= 0m))
        {
            throw Fail("fixedAmount", "A fixed donation amount is required.");
        }
    }

    private async Task ValidateSplitAsync(
        UpsertProjectDonationRequest request, decimal donationAmount, CancellationToken cancellationToken)
    {
        if (request.Temples.Count == 0)
        {
            throw Fail("temples", "At least one temple is required.");
        }

        List<long> templeIds = request.Temples.Select(t => t.TempleId).ToList();

        if (templeIds.Distinct().Count() != templeIds.Count)
        {
            throw Fail("temples", "A temple appears more than once in the split.");
        }

        if (request.Temples.Any(t => t.Amount < 0m))
        {
            throw Fail("temples", "Split amounts cannot be negative.");
        }

        int found = await db.Parties
            .CountAsync(p => templeIds.Contains(p.Id) && (p.Types & PartyType.Temple) != PartyType.None,
                cancellationToken);
        if (found != templeIds.Count)
        {
            throw Fail("temples", "One or more of the selected temples do not exist.");
        }

        if (request.Temples.Sum(t => t.Amount) != donationAmount)
        {
            throw Fail("temples",
                $"The temple split must total the donation amount of {donationAmount:0.00}.");
        }
    }

    private async Task<ProjectDonationDto> ToDtoAsync(
        ProjectDonation donation, CancellationToken cancellationToken)
    {
        List<ProjectDonationTemple> splits = await db.ProjectDonationTemples.AsNoTracking()
            .Where(s => s.ProjectDonationId == donation.Id)
            .ToListAsync(cancellationToken);

        List<long> templeIds = splits.Select(s => s.TempleId).ToList();
        Dictionary<long, string> names = await db.Parties.AsNoTracking()
            .Where(p => templeIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var splitDtos = splits
            .Select(s => new DonationTempleSplitDto(
                s.TempleId, names.GetValueOrDefault(s.TempleId, string.Empty), s.Amount))
            .ToList();

        decimal excess = Math.Max(0m, donation.PaidAmount - donation.DonationAmount);

        return new ProjectDonationDto(
            donation.Id, donation.ProjectId, donation.Basis, donation.Percentage, donation.FixedAmount,
            donation.ContractValueSnapshot, donation.DonationAmount, donation.PaidAmount, excess,
            donation.NeedsReview, splitDtos, donation.ConcurrencyStamp);
    }

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
