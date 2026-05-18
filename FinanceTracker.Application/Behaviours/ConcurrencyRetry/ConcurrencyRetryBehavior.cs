using System.Diagnostics;
using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Services.Retry;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.ConcurrencyRetry;

public sealed class ConcurrencyRetryBehavior<TRequest, TResponse>(
	ILogger<ConcurrencyRetryBehavior<TRequest, TResponse>> logger,
	IOptions<RetryOptions> options
) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
	private readonly RetryOptions _retryOptions = options.Value;
	private static readonly Random Jitter = Random.Shared;

	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken = default)
	{
		for (int attempt = 0; attempt <= _retryOptions.MaxRetries; attempt++)
		{
			try
			{
				return await next(t: cancellationToken);
			}
			catch (ConcurrencyConflictException exception) when (attempt < _retryOptions.MaxRetries)
			{
				int delayMs = RetryDelayCalculator.Calculate(attempt: attempt, baseDelayMs: _retryOptions.BaseDelayMs, useJitter: _retryOptions.UseJitter);

				logger.ZLogWarning(exception: exception, message: $"""
					Concurrency conflict on {typeof(TRequest).Name} {exception.Id}
					Retry {attempt + 1}/{_retryOptions.MaxRetries} in {delayMs}ms.
				""");

				await Task.Delay(millisecondsDelay: delayMs, cancellationToken: cancellationToken);
			}
		}

		throw new UnreachableException();
	}
}