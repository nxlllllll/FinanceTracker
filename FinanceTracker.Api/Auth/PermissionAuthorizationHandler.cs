using System.Security.Claims;
using FinanceTracker.Core.Repositories.UserPermission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FinanceTracker.Api.Auth;

/// <summary>
/// Checks the current user's permission set (via the cached <see cref="IUserPermissionReadRepository"/>)
/// against a required <see cref="PermissionRequirement"/>
/// </summary>
public sealed class PermissionAuthorizationHandler(
	IUserPermissionReadRepository permissionReadRepository
) : AuthorizationHandler<PermissionRequirement>
{
	protected override async Task HandleRequirementAsync(
		AuthorizationHandlerContext context,
		PermissionRequirement requirement)
	{
		string? sub = context.User.FindFirstValue(claimType: JwtRegisteredClaimNames.Sub);
		if (!Guid.TryParse(input: sub, result: out Guid userId))
			return;

		IReadOnlySet<string> permissions = await permissionReadRepository.GetPermissionsAsync(userId: userId);
		if (permissions.Contains(item: requirement.Permission))
			context.Succeed(requirement: requirement);
	}
}
