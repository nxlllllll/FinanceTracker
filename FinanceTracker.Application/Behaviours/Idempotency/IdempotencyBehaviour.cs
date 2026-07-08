using System.Diagnostics;
using System.Text.Json;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Utilities.Retry;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Idempotency;

public sealed class IdempotencyBehaviour<TRequest, TResponse>(
	IIdempotencyReadRepository idempotencyReadRepository,
	IIdempotencyWriteRepository idempotencyWriteRepository,
	IUnitOfWork unitOfWork,
	IOptionsMonitor<IdempotencyOptions> options,
	IDateProvider dateProvider,
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

		IdempotencyOptions currentOptions = options.CurrentValue;
		DateTimeOffset now = dateProvider.UtcNow;

		IdempotencyEntry? entry = await idempotencyReadRepository.GetAsync(
			idempotencyKey: idempotent.IdempotencyKey,
			commandType: commandType,
			userId: userId,
			ct: cancellationToken
		);

		if (entry is not null)
		{
			return await HandleExistingEntryAsync(
				entry: entry,
				idempotent: idempotent,
				commandType: commandType,
				userId: userId,
				options: currentOptions,
				now: now,
				cancellationToken: cancellationToken
			);
		}

		DateTimeOffset expiresAt = now.AddHours(hours: currentOptions.ExpiryHours);
		bool reserved = await idempotencyWriteRepository.TryReserveAsync(
			idempotencyKey: idempotent.IdempotencyKey,
			commandType: commandType,
			userId: userId,
			reservedAt: now,
			expiresAt: expiresAt,
			ct: cancellationToken
		);

		if (!reserved)
		{
			logger.ZLogInformation(message: $"[Idempotency] Key {idempotent.IdempotencyKey} is in-flight, waiting for result.");
			return await PollForCompletionAsync(
				idempotent: idempotent,
				commandType: commandType,
				userId: userId,
				options: currentOptions,
				cancellationToken: cancellationToken
			);
		}

		TResponse response;
		try
		{
			response = await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				TResponse result = await next(t: cancellationToken);

				if (result is IResult { IsSuccess: true })
				{
					await idempotencyWriteRepository.CompleteAsync(
						idempotencyKey: idempotent.IdempotencyKey,
						commandType: commandType,
						userId: userId,
						responseJson: JsonSerializer.Serialize(value: result, options: FinanceTrackerJsonOptions.Application),
						ct: cancellationToken
					);
				}

				return result;
			}, ct: cancellationToken);
		}
		catch
		{
			await idempotencyWriteRepository.DeleteAsync(
				idempotencyKey: idempotent.IdempotencyKey,
				commandType: commandType,
				userId: userId,
				ct: CancellationToken.None
			);

			logger.ZLogWarning(message: $"[Idempotency] Released key {idempotent.IdempotencyKey} for {typeof(TRequest).Name} — handler threw, client may retry.");

			throw;
		}

		if (response is IResult { IsSuccess: true })
		{
			logger.ZLogDebug(message: $"[Idempotency] Completed key {idempotent.IdempotencyKey} for {typeof(TRequest).Name} (expires: {expiresAt:O}).");
		}
		else
		{
			await idempotencyWriteRepository.DeleteAsync(
				idempotencyKey: idempotent.IdempotencyKey,
				commandType: commandType,
				userId: userId,
				ct: cancellationToken
			);

			logger.ZLogWarning(message: $"[Idempotency] Released key {idempotent.IdempotencyKey} for {typeof(TRequest).Name} — command failed, client may retry.");
		}

		return response;
	}

	private async Task<TResponse> HandleExistingEntryAsync(
		IdempotencyEntry entry,
		IIdempotentCommand idempotent,
		string commandType,
		Guid userId,
		IdempotencyOptions options,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		if (!String.IsNullOrWhiteSpace(value: entry.ResponseJson))
		{
			logger.ZLogInformation(message: $"[Idempotency] Returning cached result for {typeof(TRequest).Name} (key: {idempotent.IdempotencyKey}).");
			return JsonSerializer.Deserialize<TResponse>(json: entry.ResponseJson, options: FinanceTrackerJsonOptions.Application)!;
		}

		TimeSpan age = now - entry.ReservedAt;
		if (age.TotalSeconds >= options.AbandonedAfterSeconds)
		{
			logger.ZLogWarning(message: $"[Idempotency] Key {idempotent.IdempotencyKey} is abandoned (age: {age.TotalSeconds:F0}s). Deleting and retrying.");

			await idempotencyWriteRepository.DeleteAsync(
				idempotencyKey: idempotent.IdempotencyKey,
				commandType: commandType,
				userId: userId,
				ct: cancellationToken
			);

			return TResponse.CreateFailure(error: new IdempotencyAbandonedException(
				message: $"Idempotency key {idempotent.IdempotencyKey} was abandoned. The original request did not complete. Please retry.")
			);
		}

		logger.ZLogInformation(message: $"[Idempotency] Key {idempotent.IdempotencyKey} is in-flight (age: {age.TotalSeconds:F0}s), waiting for result.");
		return await PollForCompletionAsync(
			idempotent: idempotent,
			commandType: commandType,
			userId: userId,
			options: options,
			cancellationToken: cancellationToken
		);
	}

	private async Task<TResponse> PollForCompletionAsync(
		IIdempotentCommand idempotent,
		string commandType,
		Guid userId,
		IdempotencyOptions options,
		CancellationToken cancellationToken)
	{
		int attempt = 0;

		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.ElapsedMilliseconds < options.InFlightMaxWaitMs)
		{
			int delay = Math.Min(
				val1: RetryDelayCalculator.Calculate(
					attempt: attempt,
					baseDelayMs: options.InFlightInitialDelayMs,
					useJitter: options.UseJitter
				),
				val2: options.InFlightMaxDelayMs
			);

			await Task.Delay(millisecondsDelay: delay, cancellationToken);
			++attempt;

			IdempotencyEntry? entry = await idempotencyReadRepository.GetAsync(
				idempotencyKey: idempotent.IdempotencyKey,
				commandType: commandType,
				userId: userId,
				ct: cancellationToken
			);

			if (entry is null)
			{
				logger.ZLogWarning(message: $"[Idempotency] Key {idempotent.IdempotencyKey} disappeared during poll — likely abandoned and deleted by another request.");
				return TResponse.CreateFailure(error: new IdempotencyAbandonedException(
					message: $"Idempotency key {idempotent.IdempotencyKey} was abandoned during processing. Please retry.")
				);
			}

			if (!String.IsNullOrWhiteSpace(value: entry.ResponseJson))
			{
				logger.ZLogInformation(message: $"[Idempotency] Key {idempotent.IdempotencyKey} completed after {stopwatch.ElapsedMilliseconds}ms wait.");
				return JsonSerializer.Deserialize<TResponse>(json: entry.ResponseJson, options: FinanceTrackerJsonOptions.Application)!;
			}

			TimeSpan age = dateProvider.UtcNow - entry.ReservedAt;
			if (!(age.TotalSeconds >= options.AbandonedAfterSeconds))
				continue;

			logger.ZLogWarning(message: $"[Idempotency] Key {idempotent.IdempotencyKey} became abandoned during poll (age: {age.TotalSeconds:F0}s). Deleting.");

			await idempotencyWriteRepository.DeleteAsync(
				idempotencyKey: idempotent.IdempotencyKey,
				commandType: commandType,
				userId: userId,
				ct: cancellationToken
			);

			return TResponse.CreateFailure(error: new IdempotencyAbandonedException(
				message: $"Idempotency key {idempotent.IdempotencyKey} was abandoned. The original request did not complete. Please retry.")
			);
		}

		logger.ZLogWarning(message: $"[Idempotency] Key {idempotent.IdempotencyKey} timed out waiting for in-flight result after {options.InFlightMaxWaitMs}ms.");
		return TResponse.CreateFailure(error: new IdempotencyTimeoutException(
			message: $"Idempotency key {idempotent.IdempotencyKey} timed out waiting for an in-flight request to complete.")
		);
	}
}
