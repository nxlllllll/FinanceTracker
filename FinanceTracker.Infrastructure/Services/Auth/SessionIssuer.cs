using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.UserSession;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Token;

namespace FinanceTracker.Infrastructure.Services.Auth;

public sealed class SessionIssuer(
	ITokenService tokenService,
	IUserSessionWriteRepository userSessionWriteRepository,
	IDateProvider dateProvider
) : ISessionIssuer
{
	public async Task<TokenResponse> IssueAsync(
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

		return new TokenResponse(
			AccessToken: accessToken.Token,
			RefreshToken: refreshToken,
			AccessTokenExpiresAt: accessToken.ExpiresAt
		);
	}
}