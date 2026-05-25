using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.UserSession;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Token;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Users.Commands.RevokeToken;

public sealed class RevokeTokenHandler(
	IUserSessionReadRepository userSessionReadRepository,
	IUserSessionWriteRepository userSessionWriteRepository,
	ITokenService tokenService,
	IDateProvider dateProvider
) : IRequestHandler<RevokeTokenCommand, Result<Unit, DomainException>>
{
	public async Task<Result<Unit, DomainException>> Handle(
		RevokeTokenCommand command,
		CancellationToken ct = default)
	{
		string tokenHash = tokenService.HashRefreshToken(refreshToken: command.RefreshToken);

		UserSession? session = await userSessionReadRepository.GetByRefreshTokenHashAsync(tokenHash: tokenHash, ct: ct);

		if (session is null || !session.IsActive)
			return Result<Unit, DomainException>.Success(value: Unit.Default);

		await userSessionWriteRepository.RevokeAsync(
			sessionId: session.Id,
			revokedAt: dateProvider.UtcNow,
			ct: ct
		);

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
}
