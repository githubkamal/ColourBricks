using ColourBricks.Domain.Common;

namespace ColourBricks.Infrastructure.Identity;

/// <summary>A named set of permissions (BRD §60). A user holds exactly one role.</summary>
[Auditable("roles")]
public sealed class Role : BaseEntity
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>System roles (Administrator) cannot be deleted and keep a core permission floor.</summary>
    public bool IsSystem { get; set; }
}
