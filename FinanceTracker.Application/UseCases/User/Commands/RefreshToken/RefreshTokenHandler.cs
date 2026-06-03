using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Token;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Commands.RefreshToken;

public sealed class RefreshTokenHandler(
	IUserAuthRepository userAuthRepository,
	IUserSessionReadRepository userSessionReadRepository,
	IUserSessionWriteRepository userSessionWriteRepository,
	ITokenService tokenService,
	ISessionIssuer sessionIssuer,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider
) : IRequestHandler<RefreshTokenCommand, Result<SessionToken, DomainException>>
{
	public async Task<Result<SessionToken, DomainException>> Handle(
		RefreshTokenCommand command,
		CancellationToken ct = default)
	{
		string tokenHash = tokenService.HashRefreshToken(refreshToken: command.RefreshToken);

		Result<SessionToken, DomainException> result = Result<SessionToken, DomainException>.Failure(error: new InvalidTokenException());

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			UserSession? session = await userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: tokenHash, ct: ct);

			if (session is null || !session.IsActive(now: dateProvider.UtcNow))
				return;

			Core.Domains.User.User? user = await userAuthRepository.GetByIdAsync(userId: session.UserId, ct: ct);

			if (user is null)
				return;

			await userSessionWriteRepository.RevokeAsync(
				sessionId: session.Id,
				revokedAt: dateProvider.UtcNow,
				ct: ct
			);

			SessionToken sessionToken = await sessionIssuer.IssueAsync(user: user, ct: ct);
			result = Result<SessionToken, DomainException>.Success(value: sessionToken);
		}, ct: ct);

		return result;
	}
}