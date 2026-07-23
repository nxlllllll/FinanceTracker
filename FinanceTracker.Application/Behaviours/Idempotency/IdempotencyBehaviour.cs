using System.Text.Json;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Idempotency;

public sealed class IdempotencyBehaviour<TRequest, TResponse>(
	IIdempotencyReservationCoordinator coordinator,
	IIdempotencyWriteRepository idempotencyWriteRepository,
	IUnitOfWork unitOfWork,
	ILogger<IdempotencyBehaviour<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
	where TResponse : IResult<TResponse, DomainException>
{
	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken = default)
	{
		if (request is not IIdempotentCommand idempotent)
			return await next(t: cancellationToken);

		if (idempotent.IdempotencyKey == Guid.Empty)
		{
			logger.ZLogWarning(message: $"[Idempotency] {typeof(TRequest).Name} has empty IdempotencyKey.");
			return TResponse.CreateFailure(error: new EmptyIdempotencyKeyException(
				message: $"{typeof(TRequest).Name} implements IIdempotentCommand but IdempotencyKey is Guid.Empty.")
			);
		}

		string commandType = typeof(TRequest).Name;
		Guid userId = request is IUserScopedRequest scoped ? scoped.UserId : Guid.Empty;

		IdempotencyAcquisition acquisition = await coordinator.AcquireAsync(
			idempotencyKey: idempotent.IdempotencyKey,
			commandType: commandType,
			userId: userId,
			ct: cancellationToken
		);

		if (acquisition.Kind == IdempotencyAcquisitionKind.CachedResponse)
		{
			logger.ZLogInformation(message: $"[Idempotency] Returning cached result for {typeof(TRequest).Name} (key: {idempotent.IdempotencyKey}).");
			return JsonSerializer.Deserialize<TResponse>(
				json: acquisition.CachedResponseJson!,
				options: FinanceTrackerJsonOptions.Application
			)!;
		}

		if (acquisition.Kind == IdempotencyAcquisitionKind.Failed)
			return TResponse.CreateFailure(error: acquisition.Error!);

		return await ExecuteAndCompleteAsync(
			idempotent: idempotent,
			commandType: commandType,
			userId: userId,
			reservationId: acquisition.ReservationId,
			next: next,
			cancellationToken: cancellationToken
		);
	}

	private async Task<TResponse> ExecuteAndCompleteAsync(
		IIdempotentCommand idempotent,
		string commandType,
		Guid userId,
		Guid reservationId,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken)
	{
		TResponse response;
		try
		{
			response = await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				TResponse result = await next(t: cancellationToken);

				if (result is IResult { IsSuccess: true })
				{
					bool completed = await idempotencyWriteRepository.CompleteAsync(
						idempotencyKey: idempotent.IdempotencyKey,
						commandType: commandType,
						userId: userId,
						reservationId: reservationId,
						responseJson: JsonSerializer.Serialize(value: result, options: FinanceTrackerJsonOptions.Application),
						ct: cancellationToken
					);

					if (!completed)
					{
						throw new IdempotencyReservationLostException(
							message: $"Idempotency key {idempotent.IdempotencyKey} was reclaimed by another request before this one could complete."
						);
					}
				}

				return result;
			}, ct: cancellationToken);
		}
		catch (IdempotencyReservationLostException ex)
		{
			await ReleaseAsync(
				idempotent: idempotent,
				commandType: commandType,
				userId: userId,
				reservationId: reservationId,
				ct: CancellationToken.None
			);

			logger.ZLogWarning(message: $"""
				[Idempotency] Key {idempotent.IdempotencyKey} for {typeof(TRequest).Name} was reclaimed mid-flight — the underlying change was rolled back.
			""");

			return TResponse.CreateFailure(error: ex);
		}
		catch
		{
			await ReleaseAsync(
				idempotent: idempotent,
				commandType: commandType,
				userId: userId,
				reservationId: reservationId,
				ct: CancellationToken.None
			);

			logger.ZLogWarning(message: $"[Idempotency] Released key {idempotent.IdempotencyKey} for {typeof(TRequest).Name} — handler threw, client may retry.");

			throw;
		}

		if (response is IResult { IsSuccess: true })
		{
			logger.ZLogDebug(message: $"[Idempotency] Completed key {idempotent.IdempotencyKey} for {typeof(TRequest).Name}.");
			return response;
		}

		await ReleaseAsync(
			idempotent: idempotent,
			commandType: commandType,
			userId: userId,
			reservationId: reservationId,
			ct: cancellationToken
		);

		logger.ZLogWarning(message: $"[Idempotency] Released key {idempotent.IdempotencyKey} for {typeof(TRequest).Name} — command failed, client may retry.");

		return response;
	}

	private Task ReleaseAsync(
		IIdempotentCommand idempotent,
		string commandType,
		Guid userId,
		Guid reservationId,
		CancellationToken ct
	) => idempotencyWriteRepository.DeleteAsync(
		idempotencyKey: idempotent.IdempotencyKey,
		commandType: commandType,
		userId: userId,
		reservationId: reservationId,
		ct: ct
	);
}
