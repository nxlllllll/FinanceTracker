using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions;
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
		{
			logger.ZLogInformation(message: $"[Idempotency] Returning cached result for {typeof(TRequest).Name} (key: {idempotent.IdempotencyKey}).");
			return JsonSerializer.Deserialize<TResponse>(json: cached, options: FinanceTrackerJsonOptions.Application)!;
		}

		TResponse response = await next(t: cancellationToken);

		if (response is IResult { IsSuccess: true })
		{
			DateTimeOffset expiresAt = dateProvider.UtcNow.AddHours(hours: options.CurrentValue.ExpiryHours);

			await idempotencyWriteRepository.StoreAsync(
				idempotencyKey: idempotent.IdempotencyKey,
				commandType: typeof(TRequest).Name,
				responseJson: JsonSerializer.Serialize(value: response, options: FinanceTrackerJsonOptions.Application),
				expiresAt: expiresAt,
				ct: cancellationToken
			);

			logger.ZLogDebug(message: $"[Idempotency] Cached result for {typeof(TRequest).Name} (key: {idempotent.IdempotencyKey}, expires: {expiresAt:O}).");
		}

		return response;
	}
}
