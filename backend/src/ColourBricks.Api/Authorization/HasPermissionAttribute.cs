using Microsoft.AspNetCore.Authorization;

namespace ColourBricks.Api.Authorization;

/// <summary>
/// Requires the caller to hold a <c>module.action</c> permission
/// (plan.md §9): <c>[HasPermission("bank_reconciliation.reconcile")]</c>.
/// Never checks a role name.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";

    public HasPermissionAttribute(string permission)
    {
        Permission = permission;
        Policy = PolicyPrefix + permission;
    }

    public string Permission { get; }

    public static bool TryGetPermission(string policyName, out string permission)
    {
        if (policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal))
        {
            permission = policyName[PolicyPrefix.Length..];
            return permission.Length > 0;
        }

        permission = string.Empty;
        return false;
    }
}
