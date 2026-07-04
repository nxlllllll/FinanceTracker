using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Token;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.User.Commands.RevokeToken;

public sealed class RevokeTokenHandler(
	IUserSessionReadRepository userSessionReadRepository,
	IUserSessionWriteRepository userSessionWriteRepository,
	ITokenService tokenService,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider
) : IRequestHandler<RevokeTokenCommand, Result<Unit, AppException>>
{
	public async Task<Result<Unit, AppException>> Handle(
		RevokeTokenCommand command,
		CancellationToken ct = default)
	{
		string tokenHash = tokenService.HashRefreshToken(refreshToken: command.RefreshToken);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			UserSession? session = await userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: tokenHash, ct: ct);

			if (session is null || !session.IsActive(now: dateProvider.UtcNow))
				return;

			await userSessionWriteRepository.RevokeAsync(
				sessionId: session.Id,
				revokedAt: dateProvider.UtcNow,
				ct: ct
			);
		}, ct: ct);

		return Result<Unit, AppException>.Success(value: Unit.Default);
	}
}
