using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Utilities.Retry;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Retry;

/// <summary>
/// MediatR pipeline behaviour that automatically retries a request when a
/// <see cref="ConcurrencyConflictException"/> is thrown, using exponential backoff with optional jitter.
/// <para>
/// Applies to all requests in the pipeline. The retry count and delay are configured
/// via <see cref="RetryOptions"/>.
/// </para>
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
			logging: (exception, attempt, delay) => logger.ZLogWarning(exception: exception, message: $"""
				Concurrency conflict on {typeof(TRequest).Name} {exception.Id}
				Retry {attempt + 1}/{currentOptions.MaxRetries} in {delay}ms.
			"""),
			maxRetries: currentOptions.MaxRetries,
			baseDelayMs: currentOptions.BaseDelayMs,
			useJitter: currentOptions.UseJitter,
			ct: cancellationToken
		);
	}
}