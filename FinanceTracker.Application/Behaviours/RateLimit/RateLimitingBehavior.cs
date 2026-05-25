using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Services.RateLimit;
using MediatR;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.Behaviours.RateLimit;

/// <summary>
/// Enforces per-user rate limiting for requests implementing <see cref="IUserScopedRequest"/>.
/// </summary>
/// <remarks>
/// Intentionally throws <see cref="RateLimitExceededException"/> instead of returning
/// <c>Result.Failure</c> when the limit is exceeded.
/// </remarks>
/// <exception cref="RateLimitExceededException">
/// Thrown when the request exceeds the configured rate limit for the user.
/// </exception>
public sealed class RateLimitingBehavior<TRequest, TResponse>(
	IRateLimiter rateLimiter,
	IOptionsMonitor<RateLimitOptions> options
) : IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
{
	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken = default)
	{
		if (request is not IUserScopedRequest userScopedRequest)
			return await next(t: cancellationToken);

		RateLimitOptions currentOptions = options.CurrentValue;

		string key = $"ratelimit:{typeof(TRequest).Name}:{userScopedRequest.UserId}";

		bool isAllowed = await rateLimiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: currentOptions.RequestsPerWindow,
			windowSeconds: currentOptions.WindowSeconds,
			ct: cancellationToken
		);

		if (!isAllowed)
			throw new RateLimitExceededException(commandName: typeof(TRequest).Name);

		return await next(t: cancellationToken);
	}
}
