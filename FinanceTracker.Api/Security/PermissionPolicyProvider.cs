using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.Security;

/// <summary>
/// Resolves policy names of the form "permission:{resource}:{action}" (e.g. "permission:account:write")
/// into a <see cref="PermissionRequirement"/> on the fly — no per-permission policy registration needed.
/// Delegates any other policy name to the default provider.
/// </summary>
public sealed class PermissionPolicyProvider(
	IOptions<AuthorizationOptions> options
) : IAuthorizationPolicyProvider
{
	private const string Prefix = "permission:";

	private readonly DefaultAuthorizationPolicyProvider _fallback = new DefaultAuthorizationPolicyProvider(options: options);

	private readonly ConcurrentDictionary<string, AuthorizationPolicy> _permissionPolicies =
		new ConcurrentDictionary<string, AuthorizationPolicy>(comparer: StringComparer.Ordinal);

	public bool AllowsCachingPolicies => true;

	public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
	{
		if (!policyName.StartsWith(value: Prefix, comparisonType: StringComparison.OrdinalIgnoreCase))
			return _fallback.GetPolicyAsync(policyName: policyName);

		AuthorizationPolicy policy = _permissionPolicies.GetOrAdd(key: policyName, valueFactory: BuildPermissionPolicy);

		return Task.FromResult<AuthorizationPolicy?>(result: policy);
	}

	public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

	public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

	private static AuthorizationPolicy BuildPermissionPolicy(string policyName)
	{
		string permission = policyName[Prefix.Length..];

		return new AuthorizationPolicyBuilder()
			.AddRequirements(requirements: new PermissionRequirement(permission: permission))
			.Build();
	}
}
