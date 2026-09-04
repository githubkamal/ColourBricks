using ColourBricks.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;

namespace ColourBricks.Api.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>
/// Succeeds when the authenticated user's flattened <c>permissions</c> claim contains
/// the required key (or the wildcard <c>*</c>). Never inspects role names.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask; // unauthenticated -> 401 challenge
        }

        string? granted = context.User.FindFirst(JwtTokenGenerator.PermissionsClaim)?.Value;
        if (granted is not null && Contains(granted, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool Contains(string grantedClaim, string permission) =>
        grantedClaim == JwtTokenGenerator.AllPermissions
        || grantedClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(permission);
}
