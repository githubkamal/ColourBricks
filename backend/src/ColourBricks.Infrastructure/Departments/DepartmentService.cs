using ColourBricks.Application.Departments;
using ColourBricks.Domain.Departments;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Departments;

public sealed class DepartmentService(AppDbContext db) : IDepartmentService
{
    public async Task<IReadOnlyList<DepartmentDto>> ListAsync(
        bool includeInactive, CancellationToken cancellationToken)
    {
        IQueryable<Department> query = db.Departments.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(d => d.IsActive);
        }

        return await query
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Name, d.IsActive, d.ConcurrencyStamp))
            .ToListAsync(cancellationToken);
    }

    public async Task<DepartmentDto?> GetAsync(long id, CancellationToken cancellationToken)
    {
        Department? department = await db.Departments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        return department is null ? null : ToDto(department);
    }

    public async Task<DepartmentDto> CreateAsync(
        CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        string norm = NameNormalizer.Normalize(request.Name);

        Department? exact = await db.Departments
            .FirstOrDefaultAsync(d => d.NormalisedName == norm, cancellationToken);
        if (exact is not null)
        {
            throw new DepartmentExactDuplicateException(exact.Id, exact.Name);
        }

        var department = new Department { Name = request.Name.Trim(), NormalisedName = norm };
        db.Departments.Add(department);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            Department? raced = await db.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.NormalisedName == norm, cancellationToken);
            throw new DepartmentExactDuplicateException(raced?.Id ?? 0, request.Name);
        }

        return ToDto(department);
    }

    public async Task<DepartmentDto?> UpdateAsync(
        long id, UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        Department? department = await db.Departments
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (department is null)
        {
            return null;
        }

        string norm = NameNormalizer.Normalize(request.Name);
        db.Entry(department).Property(d => d.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;

        department.Name = request.Name.Trim();
        department.NormalisedName = norm;
        department.IsActive = request.IsActive;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            Department? clash = await db.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.NormalisedName == norm && d.Id != id, cancellationToken);
            throw new DepartmentExactDuplicateException(clash?.Id ?? 0, request.Name);
        }

        return ToDto(department);
    }

    private static DepartmentDto ToDto(Department d) => new(d.Id, d.Name, d.IsActive, d.ConcurrencyStamp);
}
