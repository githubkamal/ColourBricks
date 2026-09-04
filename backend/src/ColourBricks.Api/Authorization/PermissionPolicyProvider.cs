using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ColourBricks.Api.Authorization;

/// <summary>
/// Materialises a policy on demand for every <c>perm:&lt;key&gt;</c> policy name that
/// <see cref="HasPermissionAttribute"/> produces, so permissions don't have to be
/// registered one by one. Everything else falls back to the default provider.
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (HasPermissionAttribute.TryGetPermission(policyName, out string permission))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}
