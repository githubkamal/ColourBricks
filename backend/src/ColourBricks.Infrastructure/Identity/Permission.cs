using ColourBricks.Domain.Common;

namespace ColourBricks.Infrastructure.Identity;

/// <summary>
/// One <c>module.action</c> permission string (plan.md §9). The full catalogue is
/// every module in BRD §3 crossed with every action.
/// </summary>
public sealed class Permission : BaseEntity
{
    /// <summary>e.g. <c>vendors.edit</c>, <c>bank_reconciliation.reconcile</c>. Unique.</summary>
    public required string Key { get; set; }

    public required string Module { get; set; }

    public required string Action { get; set; }

    public string? Description { get; set; }
}
