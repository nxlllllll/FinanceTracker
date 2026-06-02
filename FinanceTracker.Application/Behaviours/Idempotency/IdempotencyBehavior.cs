using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Idempotency;

public sealed class IdempotencyBehavior<TRequest, TResponse>(
	IIdempotencyReadRepository idempotencyReadRepository,
	IIdempotencyWriteRepository idempotencyWriteRepository,
	IOptionsMonitor<IdempotencyOptions> options,
	IDateProvider dateProvider,
	ILogger<IdempotencyBehavior<TRequest, TResponse>> logger
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
			return TResponse.CreateFailure(error: new EmptyIdempotentException(message: $"{typeof(TRequest).Name} implements IIdempotentCommand but IdempotencyKey is Guid.Empty."));
		}

		string? cached = await idempotencyReadRepository.GetAsync(idempotencyKey: idempotent.IdempotencyKey, ct: cancellationToken);
		if (cached is not null)
			return await HandleExistingEntryAsync(cached: cached, idempotent: idempotent, cancellationToken: cancellationToken);

		DateTimeOffset expiresAt = dateProvider.UtcNow.AddHours(hours: options.CurrentValue.ExpiryHours);
		bool reserved = await idempotencyWriteRepository.TryReserveAsync(
			idempotencyKey: idempotent.IdempotencyKey,
			commandType: typeof(TRequest).Name,
			expiresAt: expiresAt,
			ct: cancellationToken
		);

		if (!reserved)
		{
			logger.ZLogInformation(message: $"[Idempotency] Key {idempotent.IdempotencyKey} is in-flight, waiting for result.");
			return await PollForCompletionAsync(idempotent: idempotent, cancellationToken: cancellationToken);
		}

		TResponse response = await next(t: cancellationToken);

		if (response is IResult { IsSuccess: true })
		{
			await idempotencyWriteRepository.CompleteAsync(
				idempotencyKey: idempotent.IdempotencyKey,
				responseJson: JsonSerializer.Serialize(value: response, options: FinanceTrackerJsonOptions.Application),
				ct: cancellationToken
			);

			logger.ZLogDebug(message: $"[Idempotency] Completed key {idempotent.IdempotencyKey} for {typeof(TRequest).Name} (expires: {expiresAt:O}).");
		}
		else
		{
			await idempotencyWriteRepository.DeleteAsync(
				idempotencyKey: idempotent.IdempotencyKey,
				ct: cancellationToken
			);

			logger.ZLogWarning(message: $"[Idempotency] Released key {idempotent.IdempotencyKey} for {typeof(TRequest).Name} — command failed, client may retry.");
		}
		
		return response;
	}

	private async Task<TResponse> HandleExistingEntryAsync(
		string cached,
		IIdempotentCommand idempotent,
		CancellationToken cancellationToken)
	{
		if (String.IsNullOrWhiteSpace(value: cached))
		{
			logger.ZLogInformation(message: $"[Idempotency] Key {idempotent.IdempotencyKey} is in-flight, waiting for result.");
			return await PollForCompletionAsync(idempotent: idempotent, cancellationToken: cancellationToken);
		}

		logger.ZLogInformation(message: $"[Idempotency] Returning cached result for {typeof(TRequest).Name} (key: {idempotent.IdempotencyKey}).");
		return JsonSerializer.Deserialize<TResponse>(json: cached, options: FinanceTrackerJsonOptions.Application)!;
	}

	private async Task<TResponse> PollForCompletionAsync(
		IIdempotentCommand idempotent,
		CancellationToken cancellationToken)
	{
		IdempotencyOptions currentOptions = options.CurrentValue;
		
		int elapsed = 0;
		int delay = currentOptions.InFlightInitialDelayMs;

		while (elapsed < currentOptions.InFlightMaxWaitMs)
		{
			await Task.Delay(millisecondsDelay: delay, cancellationToken);
			elapsed += delay;
			delay = Math.Min(delay * 2, currentOptions.InFlightMaxDelayMs);

			string? result = await idempotencyReadRepository.GetAsync(idempotencyKey: idempotent.IdempotencyKey, ct: cancellationToken);
			if (String.IsNullOrWhiteSpace(value: result))
				continue;

			logger.ZLogInformation(message: $"[Idempotency] Key {idempotent.IdempotencyKey} completed after {elapsed}ms wait.");
			return JsonSerializer.Deserialize<TResponse>(json: result, options: FinanceTrackerJsonOptions.Application)!;
		}

		logger.ZLogWarning(message: $"[Idempotency] Key {idempotent.IdempotencyKey} timed out waiting for in-flight result after {currentOptions.InFlightMaxWaitMs}ms.");
		return TResponse.CreateFailure(error: new EmptyIdempotentException(message: $"Idempotency key {idempotent.IdempotencyKey} timed out waiting for an in-flight request to complete."));
	}
}