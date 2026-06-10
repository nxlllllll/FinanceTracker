using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Token;

namespace FinanceTracker.Infrastructure.Services.Auth;

/// <summary>
/// Creates an authenticated session by generating a JWT access token and an opaque refresh token,
/// hashing and persisting the refresh token, and returning both to the caller.
/// </summary>
public sealed class SessionIssuer(
	ITokenService tokenService,
	IUserSessionWriteRepository userSessionWriteRepository,
	IDateProvider dateProvider
) : ISessionIssuer
{
	public async Task<SessionToken> IssueAsync(
		User user,
		CancellationToken ct = default)
	{
		AccessTokenResult accessToken = tokenService.GenerateAccessToken(user: user);
		string refreshToken = tokenService.GenerateRefreshToken();
		string refreshTokenHash = tokenService.HashRefreshToken(refreshToken: refreshToken);

		UserSession session = UserSession.Create(
			userId: user.Id,
			refreshTokenHash: refreshTokenHash,
			expiresAt: tokenService.GetRefreshTokenExpiry(),
			createdAt: dateProvider.UtcNow
		);

		await userSessionWriteRepository.CreateAsync(session: session, ct: ct);

		return new SessionToken(
			AccessToken: accessToken.Token,
			RefreshToken: refreshToken,
			AccessTokenExpiresAt: accessToken.ExpiresAt
		);
	}
}
