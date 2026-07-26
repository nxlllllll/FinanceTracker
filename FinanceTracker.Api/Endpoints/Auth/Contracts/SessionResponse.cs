using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.Services.Auth;

namespace FinanceTracker.Api.Endpoints.Auth.Contracts;

/// <summary>Successful authentication response.</summary>
public sealed record SessionResponse(
	string AccessToken,
	DateTimeOffset AccessTokenExpiresAt
) : IResponseOf<SessionToken, SessionResponse>
{
	public static SessionResponse FromReadModel(SessionToken readModel) => new SessionResponse(
		AccessToken: readModel.AccessToken,
		AccessTokenExpiresAt: readModel.AccessTokenExpiresAt
	);
}
