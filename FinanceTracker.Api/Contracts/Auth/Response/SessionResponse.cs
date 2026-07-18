using FinanceTracker.Api.Contracts.Abstractions;
using FinanceTracker.Core.Services.Auth;

namespace FinanceTracker.Api.Contracts.Auth.Response;

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
