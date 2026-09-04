namespace ColourBricks.Application.Departments;

public sealed record DepartmentDto(long Id, string Name, bool IsActive, string ConcurrencyStamp);

public sealed record CreateDepartmentRequest(string Name);

public sealed record UpdateDepartmentRequest(string Name, bool IsActive, string ConcurrencyStamp);

public sealed class DepartmentExactDuplicateException(long existingId, string name)
    : Exception($"A department named '{name}' already exists.")
{
    public long ExistingId { get; } = existingId;
}
