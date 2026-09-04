using ColourBricks.Domain.Common;

namespace ColourBricks.Infrastructure.Identity;

/// <summary>Grants one <see cref="Permission"/> to one <see cref="Role"/>.</summary>
public sealed class RolePermission : BaseEntity
{
    public long RoleId { get; set; }

    public long PermissionId { get; set; }

    public Role? Role { get; set; }

    public Permission? Permission { get; set; }
}
