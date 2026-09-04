namespace ColourBricks.Application.Departments;

public interface IDepartmentService
{
    /// <summary>Active departments only unless <paramref name="includeInactive"/> is set.</summary>
    Task<IReadOnlyList<DepartmentDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<DepartmentDto?> GetAsync(long id, CancellationToken cancellationToken);

    Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken);

    Task<DepartmentDto?> UpdateAsync(long id, UpdateDepartmentRequest request, CancellationToken cancellationToken);
}
