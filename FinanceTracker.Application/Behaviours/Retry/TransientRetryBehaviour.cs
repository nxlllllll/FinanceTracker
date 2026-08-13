using FinanceTracker.Core.Observability.Metrics;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Utilities.Retry;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Retry;

/// <summary>
/// Retries a request that failed for a reason the database itself describes as worth repeating —
/// a dropped connection, a server shutting down, a lock timeout.
/// </summary>
public sealed class TransientRetryBehaviour<TRequest, TResponse>(
	ILogger<TransientRetryBehaviour<TRequest, TResponse>> logger,
	IOptionsMonitor<RetryOptions> options,
	ITransientFaultDetector transientFaultDetector
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
						value: FinanceTrackerMetrics.RetryReasons.TransientFault
					)
				);

				logger.ZLogWarning(exception: exception, message: $"""
					Transient database fault on {typeof(TRequest).Name}.
					Retry {attempt + 1}/{currentOptions.MaxRetries} in {delay}ms.
				""");
			},
			exceptionFilter: transientFaultDetector.IsTransient,
			maxRetries: currentOptions.MaxRetries,
			baseDelayMs: currentOptions.BaseDelayMs,
			useJitter: currentOptions.UseJitter,
			ct: cancellationToken
		);
	}
}
