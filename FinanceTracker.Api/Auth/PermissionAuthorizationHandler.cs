using System.Security.Claims;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Core.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FinanceTracker.Api.Auth;

public sealed class PermissionAuthorizationHandler(
	IUserPermissionReadRepository permissionReadRepository,
	IRootAuthority rootAuthority
) : AuthorizationHandler<PermissionRequirement>
{
	protected override async Task HandleRequirementAsync(
		AuthorizationHandlerContext context,
		PermissionRequirement requirement)
	{
		string? sub = context.User.FindFirstValue(claimType: JwtRegisteredClaimNames.Sub);
		if (!Guid.TryParse(input: sub, result: out Guid userId))
			return;

		if (rootAuthority.IsRoot(userId: userId))
		{
			context.Succeed(requirement: requirement);
			return;
		}

		IReadOnlySet<string> permissions = await permissionReadRepository.GetPermissionsAsync(userId: userId);

		if (permissions.Contains(item: requirement.Permission))
			context.Succeed(requirement: requirement);
	}
}
