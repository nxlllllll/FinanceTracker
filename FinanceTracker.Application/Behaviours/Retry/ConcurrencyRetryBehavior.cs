using System.Diagnostics;
using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Utilities.Retry;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Retry;

public sealed class ConcurrencyRetryBehavior<TRequest, TResponse>(
	ILogger<ConcurrencyRetryBehavior<TRequest, TResponse>> logger,
	IOptions<RetryOptions> options
) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
	private readonly RetryOptions _retryOptions = options.Value;

	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken = default)
	{
		return await RetryDelayCalculator.ExecuteWithRetryAsync(
			operation: async ct => await next(t: ct),
			logging: (exception, attempt, delay) => logger.ZLogWarning(exception: exception, message: $"""
				Concurrency conflict on {typeof(TRequest).Name} {exception.Id}
				Retry {attempt + 1}/{_retryOptions.MaxRetries} in {delay}ms.
			"""),
			maxRetries: _retryOptions.MaxRetries,
			baseDelayMs: _retryOptions.BaseDelayMs, 
			useJitter: _retryOptions.UseJitter,
			ct: cancellationToken
		);
	}
}