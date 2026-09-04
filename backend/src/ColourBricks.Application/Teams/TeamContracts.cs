using ColourBricks.Application.Parties;

namespace ColourBricks.Application.Teams;

/// <summary>
/// A subcontractor team — a <c>Party</c> that plays the Subcontractor role and is
/// assigned to a <c>Department</c> (BRD §9). The team carries no project link; it
/// works across many projects through transaction records (BRD §70 rule 9).
/// </summary>
public sealed record TeamDto(
    long Id,
    string Name,
    long? DepartmentId,
    string? DepartmentName,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? Address,
    string? PaymentTerms,
    string? BankDetails,
    bool IsActive,
    string ConcurrencyStamp);

/// <summary>Teams grouped under their department, for the grouped picker (BRD §8).</summary>
public sealed record TeamGroup(long? DepartmentId, string DepartmentName, IReadOnlyList<TeamDto> Teams);

public sealed record CreateTeamRequest(
    string Name,
    long DepartmentId,
    string? ContactPerson = null,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    string? PaymentTerms = null,
    string? BankDetails = null);

public sealed record UpdateTeamRequest(
    string Name,
    long DepartmentId,
    bool IsActive,
    string ConcurrencyStamp,
    string? ContactPerson = null,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    string? PaymentTerms = null,
    string? BankDetails = null);

/// <summary>
/// Either the created team, or (near-duplicate name, not confirmed) the possible
/// duplicates — same shape as party creation (BRD §70 rule 17).
/// </summary>
public sealed record CreateTeamResult(TeamDto? Team, IReadOnlyList<NearDuplicate> NearDuplicates)
{
    public bool RequiresConfirmation => Team is null;

    public static CreateTeamResult Created(TeamDto team) => new(team, []);

    public static CreateTeamResult NeedsConfirmation(IReadOnlyList<NearDuplicate> nearDuplicates) =>
        new(null, nearDuplicates);
}
