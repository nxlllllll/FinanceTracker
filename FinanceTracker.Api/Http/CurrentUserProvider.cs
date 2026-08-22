using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FinanceTracker.Api.Http;

public sealed class CurrentUserProvider(
	IHttpContextAccessor httpContextAccessor
) : ICurrentUserProvider
{
	public Guid UserId => GetRequiredGuidClaim(claimType: JwtRegisteredClaimNames.Sub);

	public Guid SessionId => GetRequiredGuidClaim(claimType: JwtRegisteredClaimNames.Sid);

	private Guid GetRequiredGuidClaim(string claimType)
	{
		ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
		string? raw = user?.FindFirstValue(claimType: claimType);

		if (Guid.TryParse(input: raw, result: out Guid value))
			return value;

		throw new InvalidOperationException(message: $"""
			Claim '{claimType}' is missing or invalid — the provider must only be used behind RequireAuthorization.
		""");
	}
}
