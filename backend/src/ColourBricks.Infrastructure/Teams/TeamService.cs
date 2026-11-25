using ColourBricks.Application.Parties;
using ColourBricks.Application.Teams;
using ColourBricks.Domain.Departments;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Teams;

/// <summary>
/// Teams are <see cref="Party"/> rows that play the Subcontractor role and carry a
/// <c>DepartmentId</c> (BRD §9). Creation reuses <see cref="IPartyService"/> so the
/// duplicate-name protection (BRD §70 rule 17) is identical to the vendor master.
/// </summary>
public sealed class TeamService(AppDbContext db, IPartyService parties) : ITeamService
{
    private static readonly PartyType Role = PartyType.Subcontractor;

    public async Task<IReadOnlyList<TeamDto>> ListAsync(
        long? departmentId, bool includeInactive, CancellationToken cancellationToken)
    {
        List<Party> rows = await QueryTeams(departmentId, includeInactive).ToListAsync(cancellationToken);
        Dictionary<long, string> departments = await DepartmentNamesAsync(cancellationToken);
        ILookup<long, TeamSiteDto> sites = await SitesAsync(rows.Select(r => r.Id).ToList(), cancellationToken);

        return rows
            .OrderBy(p => DepartmentName(p.DepartmentId, departments) ?? "~", StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => ToDto(p, departments, sites))
            .ToList();
    }

    public async Task<IReadOnlyList<TeamGroup>> ListGroupedAsync(
        bool includeInactive, CancellationToken cancellationToken)
    {
        IReadOnlyList<TeamDto> teams = await ListAsync(null, includeInactive, cancellationToken);

        return teams
            .GroupBy(t => (t.DepartmentId, Name: t.DepartmentName ?? "Unassigned"))
            .OrderBy(g => g.Key.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TeamGroup(g.Key.DepartmentId, g.Key.Name, g.ToList()))
            .ToList();
    }

    public async Task<TeamDto?> GetAsync(long id, CancellationToken cancellationToken)
    {
        Party? team = await db.Parties.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && (p.Types & Role) != PartyType.None, cancellationToken);
        if (team is null)
        {
            return null;
        }

        Dictionary<long, string> departments = await DepartmentNamesAsync(cancellationToken);
        ILookup<long, TeamSiteDto> sites = await SitesAsync([team.Id], cancellationToken);
        return ToDto(team, departments, sites);
    }

    public async Task<CreateTeamResult> CreateAsync(
        CreateTeamRequest request, bool confirmed, CancellationToken cancellationToken)
    {
        await RequireAssignableDepartmentAsync(request.DepartmentId, cancellationToken);
        await RequireProjectsExistAsync(request.Sites, cancellationToken);

        CreatePartyResult result = await parties.CreateAsync(
            new CreatePartyRequest(
                Name: request.Name,
                Types: [Role],
                ContactPerson: request.ContactPerson,
                Phone: request.Phone,
                Email: request.Email,
                Address: request.Address,
                BankDetails: request.BankDetails,
                PaymentTerms: request.PaymentTerms,
                DepartmentId: request.DepartmentId),
            confirmed,
            cancellationToken);

        if (result.RequiresConfirmation)
        {
            return CreateTeamResult.NeedsConfirmation(result.NearDuplicates);
        }

        Party party = await db.Parties.FirstAsync(p => p.Id == result.Party!.Id, cancellationToken);
        ApplyMasterDetails(party, request.AlternatePhone, request.WorkingArea, request.BankAccountHolderName,
            request.BankName, request.BankAccountNumber, request.BankIfsc);
        if (request.Sites is { Count: > 0 })
        {
            await ReplaceSitesAsync(party.Id, request.Sites, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        TeamDto created = (await GetAsync(party.Id, cancellationToken))!;
        return CreateTeamResult.Created(created);
    }

    public async Task<TeamDto?> UpdateAsync(
        long id, UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        Party? team = await db.Parties
            .FirstOrDefaultAsync(p => p.Id == id && (p.Types & Role) != PartyType.None, cancellationToken);
        if (team is null)
        {
            return null;
        }

        if (request.DepartmentId != team.DepartmentId)
        {
            await RequireAssignableDepartmentAsync(request.DepartmentId, cancellationToken);
        }
        else
        {
            await RequireDepartmentExistsAsync(request.DepartmentId, cancellationToken);
        }

        await RequireProjectsExistAsync(request.Sites, cancellationToken);

        db.Entry(team).Property(p => p.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;

        team.Name = request.Name.Trim();
        team.NormalisedName = NameNormalizer.Normalize(request.Name);
        team.Types |= Role;
        team.DepartmentId = request.DepartmentId;
        team.ContactPerson = request.ContactPerson;
        team.Phone = request.Phone;
        team.Email = request.Email;
        team.Address = request.Address;
        team.PaymentTerms = request.PaymentTerms;
        team.BankDetails = request.BankDetails;
        team.IsActive = request.IsActive;
        ApplyMasterDetails(team, request.AlternatePhone, request.WorkingArea, request.BankAccountHolderName,
            request.BankName, request.BankAccountNumber, request.BankIfsc);

        // Null = the caller didn't send sites (older clients) - leave them as they are.
        if (request.Sites is not null)
        {
            await ReplaceSitesAsync(team.Id, request.Sites, cancellationToken);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            Party? clash = await db.Parties.AsNoTracking()
                .FirstOrDefaultAsync(p => p.NormalisedName == team.NormalisedName && p.Id != id, cancellationToken);
            throw new PartyExactDuplicateException(clash?.Id ?? 0, request.Name);
        }

        return (await GetAsync(team.Id, cancellationToken))!;
    }

    private static void ApplyMasterDetails(
        Party party, string? alternatePhone, string? workingArea, string? holder,
        string? bankName, string? accountNumber, string? ifsc)
    {
        party.AlternatePhone = Clean(alternatePhone);
        party.WorkingArea = Clean(workingArea);
        party.BankAccountHolderName = Clean(holder);
        party.BankName = Clean(bankName);
        party.BankAccountNumber = Clean(accountNumber);
        party.BankIfsc = Clean(ifsc)?.ToUpperInvariant();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task ReplaceSitesAsync(
        long partyId, IReadOnlyList<TeamSiteInput> sites, CancellationToken cancellationToken)
    {
        List<TeamSite> existing = await db.TeamSites.Where(s => s.PartyId == partyId).ToListAsync(cancellationToken);
        db.TeamSites.RemoveRange(existing);
        foreach (TeamSiteInput site in sites)
        {
            db.TeamSites.Add(new TeamSite
            {
                PartyId = partyId,
                ProjectId = site.ProjectId is > 0 ? site.ProjectId : null,
                SiteLocation = Clean(site.SiteLocation),
                ContractValue = site.ContractValue,
            });
        }
    }

    private async Task RequireProjectsExistAsync(
        IReadOnlyList<TeamSiteInput>? sites, CancellationToken cancellationToken)
    {
        List<long> ids = (sites ?? [])
            .Where(s => s.ProjectId is > 0).Select(s => s.ProjectId!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        int found = await db.Projects.AsNoTracking().CountAsync(p => ids.Contains(p.Id), cancellationToken);
        if (found != ids.Count)
        {
            throw new ValidationException(
                [new ValidationFailure("sites", "A selected site project does not exist.")]);
        }
    }

    private async Task<ILookup<long, TeamSiteDto>> SitesAsync(
        IReadOnlyList<long> partyIds, CancellationToken cancellationToken)
    {
        List<TeamSite> rows = await db.TeamSites.AsNoTracking()
            .Where(s => partyIds.Contains(s.PartyId))
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        List<long> projectIds = rows.Where(r => r.ProjectId != null).Select(r => r.ProjectId!.Value).Distinct().ToList();
        Dictionary<long, string> projectNames = projectIds.Count == 0
            ? []
            : await db.Projects.AsNoTracking()
                .Where(p => projectIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return rows.ToLookup(
            r => r.PartyId,
            r => new TeamSiteDto(
                r.Id, r.ProjectId,
                r.ProjectId is { } pid ? projectNames.GetValueOrDefault(pid) : null,
                r.SiteLocation, r.ContractValue));
    }

    private IQueryable<Party> QueryTeams(long? departmentId, bool includeInactive)
    {
        IQueryable<Party> query = db.Parties.AsNoTracking()
            .Where(p => (p.Types & Role) != PartyType.None);

        if (departmentId is { } d)
        {
            query = query.Where(p => p.DepartmentId == d);
        }

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return query;
    }

    private async Task RequireDepartmentExistsAsync(long departmentId, CancellationToken cancellationToken)
    {
        bool exists = await db.Departments.AsNoTracking().AnyAsync(d => d.Id == departmentId, cancellationToken);
        if (!exists)
        {
            throw new ValidationException(
                [new ValidationFailure("departmentId", "The selected department does not exist.")]);
        }
    }

    private async Task RequireAssignableDepartmentAsync(long departmentId, CancellationToken cancellationToken)
    {
        bool active = await db.Departments.AsNoTracking()
            .AnyAsync(d => d.Id == departmentId && d.IsActive, cancellationToken);
        if (!active)
        {
            throw new ValidationException(
                [new ValidationFailure("departmentId", "The selected department does not exist or is inactive.")]);
        }
    }

    private async Task<Dictionary<long, string>> DepartmentNamesAsync(CancellationToken cancellationToken) =>
        await db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);

    private static string? DepartmentName(long? departmentId, IReadOnlyDictionary<long, string> names) =>
        departmentId is { } d && names.TryGetValue(d, out string? name) ? name : null;

    private static TeamDto ToDto(
        Party p, IReadOnlyDictionary<long, string> departments, ILookup<long, TeamSiteDto> sites) => new(
        p.Id, p.Name, p.DepartmentId, DepartmentName(p.DepartmentId, departments),
        p.ContactPerson, p.Phone, p.Email, p.Address, p.PaymentTerms, p.BankDetails,
        p.IsActive, p.ConcurrencyStamp,
        p.AlternatePhone, p.WorkingArea, p.BankAccountHolderName, p.BankName, p.BankAccountNumber, p.BankIfsc,
        sites[p.Id].ToList());
}
