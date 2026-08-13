using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Observability.Metrics;
using FinanceTracker.Core.Utilities.Retry;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Retry;

/// <summary>
/// Retries a request whose write lost an optimistic-concurrency race, so the handler reloads the
/// aggregate at its current version and reapplies the change.
/// </summary>
public sealed class ConcurrencyRetryBehaviour<TRequest, TResponse>(
	ILogger<ConcurrencyRetryBehaviour<TRequest, TResponse>> logger,
	IOptionsMonitor<RetryOptions> options
) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
	/// <inheritdoc/>
	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken = default)
	{
		RetryOptions currentOptions = options.CurrentValue;

		return await RetryDelayCalculator.ExecuteWithRetryAsync(
			operation: async ct => await next(t: ct),
			onError: (exception, attempt, delay) =>
			{
				FinanceTrackerMetrics.CommandRetried.Add(
					delta: 1,
					tag1: new KeyValuePair<string, object?>(
						key: FinanceTrackerMetrics.Tags.RequestType,
						value: typeof(TRequest).Name
					),
					tag2: new KeyValuePair<string, object?>(
						key: FinanceTrackerMetrics.Tags.Reason,
						value: FinanceTrackerMetrics.RetryReasons.ConcurrencyConflict
					)
				);

				logger.ZLogWarning(exception: exception, message: $"""
					Concurrency conflict {((ConcurrencyConflictException)exception).Id} on {typeof(TRequest).Name}.
					Retry {attempt + 1}/{currentOptions.MaxRetries} in {delay}ms.
				""");
			},
			exceptionFilter: exception => exception is ConcurrencyConflictException,
			maxRetries: currentOptions.MaxRetries,
			baseDelayMs: currentOptions.BaseDelayMs,
			useJitter: currentOptions.UseJitter,
			ct: cancellationToken
		);
	}
}
