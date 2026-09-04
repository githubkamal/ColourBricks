using ColourBricks.Domain.Parties;

namespace ColourBricks.Application.Parties;

public sealed record PartyDto(
    long Id,
    string Name,
    IReadOnlyList<string> Types,
    string? Category,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? Address,
    string? GstNumber,
    string? BankDetails,
    string? PaymentTerms,
    long? DepartmentId,
    bool IsActive,
    string ConcurrencyStamp);

public sealed record PartySearchItem(long Id, string Name, IReadOnlyList<string> Types, string? Category);

/// <summary>A party close enough to a new name to warrant a warning (plan.md §6).</summary>
public sealed record NearDuplicate(long Id, string Name, IReadOnlyList<string> Types);

public sealed record CreatePartyRequest(
    string Name,
    IReadOnlyList<PartyType> Types,
    string? Category = null,
    string? ContactPerson = null,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    string? GstNumber = null,
    string? BankDetails = null,
    string? PaymentTerms = null,
    long? DepartmentId = null);

/// <summary>
/// Either a created party, or (when a near-duplicate exists and the caller has not
/// confirmed) the list of possible duplicates to show the user.
/// </summary>
public sealed record CreatePartyResult(PartyDto? Party, IReadOnlyList<NearDuplicate> NearDuplicates)
{
    public bool RequiresConfirmation => Party is null;

    public static CreatePartyResult Created(PartyDto party) => new(party, []);

    public static CreatePartyResult NeedsConfirmation(IReadOnlyList<NearDuplicate> nearDuplicates) =>
        new(null, nearDuplicates);
}

public sealed class PartyExactDuplicateException(long existingId, string name)
    : Exception($"A party named '{name}' already exists.")
{
    public long ExistingId { get; } = existingId;
}
