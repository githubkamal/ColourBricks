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

        return rows
            .OrderBy(p => DepartmentName(p.DepartmentId, departments) ?? "~", StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => ToDto(p, departments))
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
        return ToDto(team, departments);
    }

    public async Task<CreateTeamResult> CreateAsync(
        CreateTeamRequest request, bool confirmed, CancellationToken cancellationToken)
    {
        await RequireAssignableDepartmentAsync(request.DepartmentId, cancellationToken);

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

        TeamDto created = (await GetAsync(result.Party!.Id, cancellationToken))!;
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

        Dictionary<long, string> departments = await DepartmentNamesAsync(cancellationToken);
        return ToDto(team, departments);
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

    private static TeamDto ToDto(Party p, IReadOnlyDictionary<long, string> departments) => new(
        p.Id, p.Name, p.DepartmentId, DepartmentName(p.DepartmentId, departments),
        p.ContactPerson, p.Phone, p.Email, p.Address, p.PaymentTerms, p.BankDetails,
        p.IsActive, p.ConcurrencyStamp);
}
