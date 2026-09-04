namespace ColourBricks.Domain.Common;

/// <summary>
/// Marks an entity whose inserts, updates and deletes are written to the audit trail
/// by the SaveChanges interceptor (plan.md §10, BRD §65).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AuditableAttribute : Attribute
{
    public AuditableAttribute(string module)
    {
        Module = module;
    }

    /// <summary>The module key recorded on each audit row, e.g. <c>users</c>.</summary>
    public string Module { get; }
}
