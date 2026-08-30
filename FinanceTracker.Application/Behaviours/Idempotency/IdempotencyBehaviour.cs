using System.Text.Json;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;
using FinanceTracker.Core.Observability.Metrics;
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
	private static readonly string RequestTypeName = typeof(TRequest).Name;

	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken = default)
	{
		if (request is not IIdempotentCommand idempotent)
			return await next(t: cancellationToken);

		if (idempotent.IdempotencyKey == Guid.Empty)
		{
			logger.ZLogWarning(message: $"[Idempotency] {RequestTypeName} has empty IdempotencyKey.");
			return TResponse.CreateFailure(error: new EmptyIdempotencyKeyException(
				message: $"{RequestTypeName} implements IIdempotentCommand but IdempotencyKey is Guid.Empty.")
			);
		}

		Guid userId = request is IUserScopedRequest scoped ? scoped.UserId : Guid.Empty;

		IdempotencyAcquisition acquisition = await coordinator.AcquireAsync(
			idempotencyKey: idempotent.IdempotencyKey,
			commandType: RequestTypeName,
			userId: userId,
			ct: cancellationToken
		);

		FinanceTrackerMetrics.IdempotencyAcquisition.Add(delta: 1, tag: new KeyValuePair<string, object?>(
			key: FinanceTrackerMetrics.Tags.Kind,
			value: acquisition.Kind switch
			{
				IdempotencyAcquisitionKind.CachedResponse => "cached_response",
				IdempotencyAcquisitionKind.Reserved => "reserved",
				IdempotencyAcquisitionKind.Failed => "failed",
				_ => "unknown"
			}
		));

		if (acquisition.Kind == IdempotencyAcquisitionKind.CachedResponse)
		{
			logger.ZLogInformation(message: $"[Idempotency] Returning cached result for {RequestTypeName} (key: {idempotent.IdempotencyKey}).");
			return JsonSerializer.Deserialize<TResponse>(
				json: acquisition.CachedResponseJson!,
				options: FinanceTrackerJsonOptions.Application
			)!;
		}

		if (acquisition.Kind == IdempotencyAcquisitionKind.Failed)
			return TResponse.CreateFailure(error: acquisition.Error!);

		return await ExecuteAndCompleteAsync(
			idempotent: idempotent,
			userId: userId,
			reservationId: acquisition.ReservationId,
			next: next,
			cancellationToken: cancellationToken
		);
	}

	private async Task<TResponse> ExecuteAndCompleteAsync(
		IIdempotentCommand idempotent,
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
						commandType: RequestTypeName,
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
				userId: userId,
				reservationId: reservationId
			);

			logger.ZLogWarning(message: $"""
				[Idempotency] Key {idempotent.IdempotencyKey} for {RequestTypeName} was reclaimed mid-flight — the underlying change was rolled back.
			""");

			return TResponse.CreateFailure(error: ex);
		}
		catch
		{
			await ReleaseAsync(
				idempotent: idempotent,
				userId: userId,
				reservationId: reservationId
			);

			logger.ZLogWarning(message: $"[Idempotency] Released key {idempotent.IdempotencyKey} for {RequestTypeName} — handler threw, client may retry.");

			throw;
		}

		if (response is IResult { IsSuccess: true })
		{
			logger.ZLogDebug(message: $"[Idempotency] Completed key {idempotent.IdempotencyKey} for {RequestTypeName}.");
			return response;
		}

		await ReleaseAsync(
			idempotent: idempotent,
			userId: userId,
			reservationId: reservationId
		);

		logger.ZLogWarning(message: $"[Idempotency] Released key {idempotent.IdempotencyKey} for {RequestTypeName} — command failed, client may retry.");

		return response;
	}

	private Task<bool> ReleaseAsync(
		IIdempotentCommand idempotent,
		Guid userId,
		Guid reservationId
	) => idempotencyWriteRepository.DeleteAsync(
		idempotencyKey: idempotent.IdempotencyKey,
		commandType: RequestTypeName,
		userId: userId,
		reservationId: reservationId,
		ct: CancellationToken.None
	);
}
