using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.RateLimit;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FinanceTracker.Application.Behaviours.RateLimit;

/// <summary>
/// Enforces per-user rate limiting for requests implementing <see cref="IUserScopedRequest"/>.
/// Returns <see cref="Result{TValue,TError}.Failure"/> with <see cref="RateLimitExceededException"/>
/// when the limit is exceeded, consistent with the rest of the pipeline.
/// </summary>
/// <remarks>
/// TODO: replace this try/catch with an <see cref="IRateLimiter"/> decorator that falls back to an
/// </remarks>
public sealed class RateLimitingBehaviour<TRequest, TResponse>(
	IRateLimiter rateLimiter,
	IOptionsMonitor<RateLimitOptions> options,
	ILogger<RateLimitingBehaviour<TRequest, TResponse>> logger
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

		string key = $"ratelimit:{typeof(TRequest).Name}:{userScopedRequest.UserId}";

		bool isAllowed;
		try
		{
			isAllowed = await rateLimiter.IsAllowedAsync(
				key: key,
				requestsPerWindow: currentOptions.RequestsPerWindow,
				windowSeconds: currentOptions.WindowSeconds,
				ct: cancellationToken
			);
		}
		catch (RedisException ex)
		{
			logger.LogWarning(exception: ex, message: "Rate limiter backing store unavailable for {RequestType} — failing open.", typeof(TRequest).Name);
			return await next(t: cancellationToken);
		}

		if (!isAllowed)
			return TResponse.CreateFailure(error: new RateLimitExceededException(commandName: typeof(TRequest).Name));

		return await next(t: cancellationToken);
	}
}
