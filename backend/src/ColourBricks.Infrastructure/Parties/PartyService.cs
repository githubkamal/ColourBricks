using ColourBricks.Application.Common.Pagination;
using ColourBricks.Application.Parties;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Parties;

public sealed class PartyService(AppDbContext db) : IPartyService
{
    private const int MaxSearchResults = 20;

    public async Task<PagedResult<PartySearchItem>> ListAsync(
        PartyType? type, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<Party> query = db.Parties.AsNoTracking();

        if (type is { } t)
        {
            query = query.Where(p => (p.Types & t) != PartyType.None);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string norm = NameNormalizer.Normalize(search);
            query = query.Where(p => p.NormalisedName.Contains(norm) || p.Name.Contains(search));
        }

        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize < 1 ? 50 : pageSize, 1, 200);

        int totalCount = await query.CountAsync(cancellationToken);
        List<Party> rows = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<PartySearchItem>.Create(rows.Select(ToSearchItem).ToList(), page, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<PartySearchItem>> SearchAsync(
        string query, PartyType? type, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit <= 0 ? MaxSearchResults : limit, 1, MaxSearchResults);
        string norm = NameNormalizer.Normalize(query);
        if (norm.Length == 0)
        {
            return [];
        }

        IQueryable<Party> candidates = db.Parties.AsNoTracking().Where(p => p.IsActive);
        if (type is { } t)
        {
            candidates = candidates.Where(p => (p.Types & t) != PartyType.None);
        }

        List<Party> matches = await candidates
            .Where(p => p.NormalisedName.Contains(norm))
            .Take(200)
            .ToListAsync(cancellationToken);

        // Rank: exact, then prefix, then contains; then alphabetical.
        return matches
            .OrderBy(p => p.NormalisedName == norm ? 0
                : p.NormalisedName.StartsWith(norm, StringComparison.Ordinal) ? 1 : 2)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(ToSearchItem)
            .ToList();
    }

    public async Task<PartyDto?> GetAsync(long id, CancellationToken cancellationToken)
    {
        Party? party = await db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return party is null ? null : ToDto(party);
    }

    public async Task<CreatePartyResult> CreateAsync(
        CreatePartyRequest request, bool confirmed, CancellationToken cancellationToken)
    {
        string norm = NameNormalizer.Normalize(request.Name);

        Party? exact = await db.Parties.FirstOrDefaultAsync(p => p.NormalisedName == norm, cancellationToken);
        if (exact is not null)
        {
            throw new PartyExactDuplicateException(exact.Id, exact.Name);
        }

        if (!confirmed)
        {
            List<NearDuplicate> nearby = (await db.Parties.AsNoTracking()
                    .Select(p => new { p.Id, p.Name, p.NormalisedName, p.Types })
                    .ToListAsync(cancellationToken))
                .Where(p => NameNormalizer.AreNearDuplicates(norm, p.NormalisedName))
                .Select(p => new NearDuplicate(p.Id, p.Name, TypeNames(p.Types)))
                .ToList();

            if (nearby.Count > 0)
            {
                return CreatePartyResult.NeedsConfirmation(nearby);
            }
        }

        var party = new Party
        {
            Name = request.Name.Trim(),
            NormalisedName = norm,
            Types = request.Types.Aggregate(PartyType.None, (acc, t) => acc | t),
            Category = request.Category,
            ContactPerson = request.ContactPerson,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            GstNumber = request.GstNumber,
            BankDetails = request.BankDetails,
            PaymentTerms = request.PaymentTerms,
            DepartmentId = request.DepartmentId,
        };

        db.Parties.Add(party);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            Party? raced = await db.Parties.AsNoTracking()
                .FirstOrDefaultAsync(p => p.NormalisedName == norm, cancellationToken);
            throw new PartyExactDuplicateException(raced?.Id ?? 0, request.Name);
        }

        return CreatePartyResult.Created(ToDto(party));
    }

    public async Task<PartyDto?> UpdateAsync(
        long id, UpdatePartyRequest request, CancellationToken cancellationToken)
    {
        Party? party = await db.Parties.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (party is null)
        {
            return null;
        }

        string norm = NameNormalizer.Normalize(request.Name);
        db.Entry(party).Property(p => p.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;

        party.Name = request.Name.Trim();
        party.NormalisedName = norm;
        party.Types = request.Types.Aggregate(PartyType.None, (acc, t) => acc | t);
        party.Category = request.Category;
        party.ContactPerson = request.ContactPerson;
        party.Phone = request.Phone;
        party.Email = request.Email;
        party.Address = request.Address;
        party.GstNumber = request.GstNumber;
        party.BankDetails = request.BankDetails;
        party.PaymentTerms = request.PaymentTerms;
        party.DepartmentId = request.DepartmentId;
        party.IsActive = request.IsActive;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            Party? clash = await db.Parties.AsNoTracking()
                .FirstOrDefaultAsync(p => p.NormalisedName == norm && p.Id != id, cancellationToken);
            throw new PartyExactDuplicateException(clash?.Id ?? 0, request.Name);
        }

        return ToDto(party);
    }

    private static IReadOnlyList<string> TypeNames(PartyType types) =>
        Enum.GetValues<PartyType>()
            .Where(t => t != PartyType.None && types.HasFlag(t))
            .Select(t => t.ToString())
            .ToList();

    private static PartySearchItem ToSearchItem(Party p) =>
        new(p.Id, p.Name, TypeNames(p.Types), p.Category, p.IsActive);

    private static PartyDto ToDto(Party p) => new(
        p.Id, p.Name, TypeNames(p.Types), p.Category, p.ContactPerson, p.Phone, p.Email,
        p.Address, p.GstNumber, p.BankDetails, p.PaymentTerms, p.DepartmentId, p.IsActive, p.ConcurrencyStamp);
}
