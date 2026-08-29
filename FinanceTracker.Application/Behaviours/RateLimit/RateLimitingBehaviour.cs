using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.RateLimit;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.RateLimit;
using MediatR;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.Behaviours.RateLimit;

/// <summary>
/// Enforces per-user rate limiting for requests implementing <see cref="IUserScopedRequest"/>.
/// Returns <see cref="Result{TValue,TError}.Failure"/> with <see cref="RateLimitExceededException"/>
/// when the limit is exceeded, consistent with the rest of the pipeline.
/// </summary>
public sealed class RateLimitingBehaviour<TRequest, TResponse>(
	IRateLimiter rateLimiter,
	IOptionsMonitor<RateLimitOptions> options
) : IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
	where TResponse : IResult<TResponse, DomainException>
{
	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken = default)
	{
		if (request is not IUserScopedRequest userScopedRequest)
			return await next(t: cancellationToken);

		RateLimitOptions currentOptions = options.CurrentValue;

		string key = RateLimitKeys.GetUser(userId: userScopedRequest.UserId);

		RateLimitResult result = await rateLimiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: currentOptions.RequestsPerWindow,
			windowSeconds: currentOptions.WindowSeconds,
			ct: cancellationToken
		);

		if (!result.IsAllowed)
			return TResponse.CreateFailure(error: new RateLimitExceededException(commandName: typeof(TRequest).Name, retryAfterSeconds: result.RetryAfterSeconds));

		return await next(t: cancellationToken);
	}
}
