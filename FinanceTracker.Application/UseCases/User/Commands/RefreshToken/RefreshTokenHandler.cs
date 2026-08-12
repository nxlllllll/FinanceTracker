using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Auth;
using FinanceTracker.Core.Observability.Metrics;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Token;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.User.Commands.RefreshToken;

public sealed class RefreshTokenHandler(
	IUserAuthRepository userAuthRepository,
	IUserSessionReadRepository userSessionReadRepository,
	IUserSessionWriteRepository userSessionWriteRepository,
	ITokenService tokenService,
	ISessionIssuer sessionIssuer,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<RefreshTokenHandler> logger
) : IRequestHandler<RefreshTokenCommand, Result<SessionToken, AppException>>
{
	internal sealed record RotateResult(
		Result<SessionToken, AppException> Result,
		Guid? CompromisedUserId
	);

	public async Task<Result<SessionToken, AppException>> Handle(
		RefreshTokenCommand command,
		CancellationToken ct = default)
	{
		string tokenHash = tokenService.HashRefreshToken(refreshToken: command.RefreshToken);

		RotateResult rotateResult = await unitOfWork.ExecuteInTransactionAsync(
			operation: () => RotateAsync(tokenHash: tokenHash, ct: ct),
			ct: ct
		);

		if (rotateResult.CompromisedUserId is { } userId)
			await PublishReuseDetectedAsync(userId: userId, ct: ct);

		return rotateResult.Result;
	}

	private async Task<RotateResult> RotateAsync(
		string tokenHash,
		CancellationToken ct)
	{
		UserSession? session = await userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: tokenHash,
			ct: ct
		);

		if (session is null)
		{
			return new RotateResult(
				Result: Result<SessionToken, AppException>.Failure(error: new InvalidTokenException()),
				CompromisedUserId: null
			);
		}

		if (!session.IsActive(now: dateProvider.UtcNow))
			return await HandleInactiveSessionAsync(session: session, ct: ct);

		return await IssueSuccessorAsync(session: session, ct: ct);
	}

	private async Task<RotateResult> HandleInactiveSessionAsync(
		UserSession session,
		CancellationToken ct)
	{
		if (session.RevokedAt is null)
		{
			return new RotateResult(
				Result: Result<SessionToken, AppException>.Failure(error: new InvalidTokenException()),
				CompromisedUserId: null
			);
		}

		if (await TryHandleLostResponseReplayAsync(session: session, ct: ct) is { } replayResult)
			return replayResult;

		FinanceTrackerMetrics.RefreshTokenReplay.Add(
			delta: 1,
			tag: new KeyValuePair<string, object?>(
				key: FinanceTrackerMetrics.Tags.Outcome,
				value: FinanceTrackerMetrics.ReplayOutcomes.ReuseDetected
			)
		);

		logger.ZLogWarning(message: $"""
			[Security] Reuse of an already-revoked refresh token detected for session {session.Id} (user {session.UserId}).
			Revoking all active sessions for this user as a precaution.
		""");

		await userSessionWriteRepository.RevokeAllAsync(
			userId: session.UserId,
			revokedAt: dateProvider.UtcNow,
			ct: ct
		);

		return new RotateResult(
			Result: Result<SessionToken, AppException>.Failure(error: new InvalidTokenException()),
			CompromisedUserId: session.UserId
		);
	}

	private async Task<RotateResult?> TryHandleLostResponseReplayAsync(
		UserSession session,
		CancellationToken ct)
	{
		TimeSpan graceWindow = tokenService.GetRefreshReplayGraceWindow();
		if (graceWindow <= TimeSpan.Zero || session.SupersededBySessionId is not { } successorId)
			return null;

		UserSession? successor = await userSessionReadRepository.GetByIdForUpdateAsync(
			sessionId: successorId,
			ct: ct
		);

		if (successor is null)
			return null;

		if (!session.WasSupersededByUnusedSession(successor: successor, now: dateProvider.UtcNow, graceWindow: graceWindow))
			return null;

		FinanceTrackerMetrics.RefreshTokenReplay.Add(
			delta: 1,
			tag: new KeyValuePair<string, object?>(
				key: FinanceTrackerMetrics.Tags.Outcome,
				value: FinanceTrackerMetrics.ReplayOutcomes.Allowed
			)
		);

		logger.ZLogInformation(message: $"""
			Refresh token for session {session.Id} (user {session.UserId}) was replayed within the grace window
			while its successor {successor.Id} was still unused — treating it as a retry of a lost response
			and issuing a replacement session.
		""");

		return await IssueSuccessorAsync(session: successor, ct: ct);
	}

	private async Task<RotateResult> IssueSuccessorAsync(
		UserSession session,
		CancellationToken ct)
	{
		Core.Domains.User.User? user = await userAuthRepository.GetByIdAsync(
			userId: session.UserId,
			ct: ct
		);

		if (user is null)
		{
			return new RotateResult(
				Result: Result<SessionToken, AppException>.Failure(error: new InvalidTokenException()),
				CompromisedUserId: null
			);
		}

		SessionToken sessionToken = await sessionIssuer.IssueAsync(user: user, ct: ct);

		await userSessionWriteRepository.SupersedeAsync(
			sessionId: session.Id,
			successorSessionId: sessionToken.SessionId,
			revokedAt: dateProvider.UtcNow,
			ct: ct
		);

		return new RotateResult(
			Result: Result<SessionToken, AppException>.Success(value: sessionToken),
			CompromisedUserId: null
		);
	}

	private async Task PublishReuseDetectedAsync(Guid userId, CancellationToken ct)
	{
		try
		{
			await publisher.Publish(notification: new RefreshTokenReuseDetectedNotification(
				UserId: userId,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish RefreshTokenReuseDetectedNotification for user {userId} after successful commit.");
		}
	}
}
