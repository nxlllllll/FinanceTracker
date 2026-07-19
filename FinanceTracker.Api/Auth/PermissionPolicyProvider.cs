using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.Auth;

/// <summary>
/// Resolves policy names of the form "permission:{resource}:{action}" (e.g. "permission:account:write")
/// into a <see cref="PermissionRequirement"/> on the fly — no per-permission policy registration needed.
/// Falls back to the default provider for any other policy name.
/// </summary>
public sealed class PermissionPolicyProvider(
	IOptions<AuthorizationOptions> options
) : DefaultAuthorizationPolicyProvider(options: options)
{
	private const string Prefix = "permission:";

	public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
	{
		if (!policyName.StartsWith(value: Prefix, comparisonType: StringComparison.OrdinalIgnoreCase))
			return await base.GetPolicyAsync(policyName: policyName);

		string permission = policyName[Prefix.Length..];

		return new AuthorizationPolicyBuilder().AddRequirements(requirements: new PermissionRequirement(permission: permission)).Build();
	}
}
