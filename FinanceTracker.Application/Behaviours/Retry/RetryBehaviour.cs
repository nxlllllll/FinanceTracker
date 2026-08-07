using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Utilities.Retry;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Retry;

/// <summary>
/// Retries a request when it fails for a reason that a second attempt
/// could get past: a version conflict, or a transient database fault.
/// </summary>
public sealed class RetryBehaviour<TRequest, TResponse>(
	ILogger<RetryBehaviour<TRequest, TResponse>> logger,
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
			onError: (exception, attempt, delay) => logger.ZLogWarning(exception: exception, message: $"""
				{Describe(exception: exception)} on {typeof(TRequest).Name}.
				Retry {attempt + 1}/{currentOptions.MaxRetries} in {delay}ms.
			"""),
			exceptionFilter: exception => exception is ConcurrencyConflictException || transientFaultDetector.IsTransient(exception: exception),
			maxRetries: currentOptions.MaxRetries,
			baseDelayMs: currentOptions.BaseDelayMs,
			useJitter: currentOptions.UseJitter,
			ct: cancellationToken
		);
	}

	private static string Describe(Exception exception)
	{
		if (exception is ConcurrencyConflictException conflict)
			return $"Concurrency conflict {conflict.Id}";

		return "Transient database fault";
	}
}
