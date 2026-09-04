using System.Security.Claims;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Core.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FinanceTracker.Api.Security;

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

		CancellationToken ct = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;

		if (await rootAuthority.IsRootAsync(userId: userId, ct: ct))
		{
			context.Succeed(requirement: requirement);
			return;
		}

		IReadOnlySet<string> permissions = await permissionReadRepository.GetPermissionsAsync(userId: userId, ct: ct);

		if (permissions.Contains(item: requirement.Permission))
			context.Succeed(requirement: requirement);
	}
}
