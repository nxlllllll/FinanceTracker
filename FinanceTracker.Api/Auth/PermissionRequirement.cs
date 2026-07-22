using Microsoft.AspNetCore.Authorization;

namespace FinanceTracker.Api.Auth;

/// <summary>
/// Requires the current user to hold a specific permission (e.g. "account:write").
/// </summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
	public string Permission { get; } = permission;
}
