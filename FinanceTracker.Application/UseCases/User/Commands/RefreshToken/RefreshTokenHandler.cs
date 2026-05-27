using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Token;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Commands.RefreshToken;

public sealed class RefreshTokenHandler(
	IUserReadRepository userReadRepository,
	IUserSessionReadRepository userSessionReadRepository,
	IUserSessionWriteRepository userSessionWriteRepository,
	ITokenService tokenService,
	ISessionIssuer sessionIssuer,
	IDateProvider dateProvider
) : IRequestHandler<RefreshTokenCommand, Result<SessionToken, DomainException>>
{
	public async Task<Result<SessionToken, DomainException>> Handle(
		RefreshTokenCommand command,
		CancellationToken ct = default)
	{
		string tokenHash = tokenService.HashRefreshToken(refreshToken: command.RefreshToken);

		UserSession? session = await userSessionReadRepository.GetByRefreshTokenHashAsync(tokenHash: tokenHash, ct: ct);

		if (session is null || !session.IsActive(now: dateProvider.UtcNow))
			return Result<SessionToken, DomainException>.Failure(error: new InvalidTokenException());

		Core.Domains.User.User? user = await userReadRepository.GetByIdAsync(userId: session.UserId, ct: ct);

		if (user is null)
			return Result<SessionToken, DomainException>.Failure(error: new InvalidTokenException());

		await userSessionWriteRepository.RevokeAsync(
			sessionId: session.Id,
			revokedAt: dateProvider.UtcNow,
			ct: ct
		);

		SessionToken response = await sessionIssuer.IssueAsync(user: user, ct: ct);
		return Result<SessionToken, DomainException>.Success(value: response);
	}
}
