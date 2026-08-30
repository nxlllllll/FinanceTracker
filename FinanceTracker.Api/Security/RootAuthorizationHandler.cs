using System.Security.Claims;
using FinanceTracker.Core.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FinanceTracker.Api.Security;

public sealed class RootAuthorizationHandler(
	IRootAuthority rootAuthority
) : AuthorizationHandler<RootRequirement>
{
	protected override async Task HandleRequirementAsync(
		AuthorizationHandlerContext context,
		RootRequirement requirement)
	{
		string? sub = context.User.FindFirstValue(claimType: JwtRegisteredClaimNames.Sub);
		if (!Guid.TryParse(input: sub, result: out Guid userId))
			return;

		CancellationToken ct = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;

		if (await rootAuthority.IsRootAsync(userId: userId, ct: ct))
			context.Succeed(requirement: requirement);
	}
}
