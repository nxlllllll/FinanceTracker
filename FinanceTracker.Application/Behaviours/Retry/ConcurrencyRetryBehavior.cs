using FinanceTracker.Core.Utilities.Retry;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Retry;

public sealed class ConcurrencyRetryBehavior<TRequest, TResponse>(
	ILogger<ConcurrencyRetryBehavior<TRequest, TResponse>> logger,
	IOptionsMonitor<RetryOptions> options
) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
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
