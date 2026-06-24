using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.RateLimit;
using MediatR;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.Behaviours.RateLimit;

/// <summary>
/// Enforces rate limiting for pre-authentication requests — login, registration, token
/// refresh/revocation — that have no <see cref="IUserScopedRequest.UserId"/> to key on yet.
/// Checks the IP-based limit (<see cref="IIpScopedRequest"/>) and the email-based limit
/// (<see cref="IEmailScopedRequest"/>) independently when the request implements either —
/// this protects against both a distributed brute force of one email from many IPs and a
/// targeted brute force of many emails from one IP. Either limit being exceeded blocks the
/// request with <see cref="RateLimitExceededException"/>, consistent with the rest of the pipeline.
/// </summary>
public sealed class AuthRateLimitingBehaviour<TRequest, TResponse>(
	IRateLimiter rateLimiter,
	IOptionsMonitor<AnonymousRateLimitOptions> options
) : IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
	where TResponse : IResult<TResponse, DomainException>
{
	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken = default)
	{
		AnonymousRateLimitOptions currentOptions = options.CurrentValue;

		if (request is IIpScopedRequest ipScopedRequest)
		{
			bool ipAllowed = await rateLimiter.IsAllowedAsync(
				key: $"ratelimit:ip:{typeof(TRequest).Name}:{ipScopedRequest.IpAddress}",
				requestsPerWindow: currentOptions.IpRequestsPerWindow,
				windowSeconds: currentOptions.IpWindowSeconds,
				ct: cancellationToken
			);

			if (!ipAllowed)
				return TResponse.CreateFailure(error: new RateLimitExceededException(commandName: typeof(TRequest).Name));
		}

		if (request is IEmailScopedRequest emailScopedRequest)
		{
			bool emailAllowed = await rateLimiter.IsAllowedAsync(
				key: $"ratelimit:email:{typeof(TRequest).Name}:{emailScopedRequest.Email.Value}",
				requestsPerWindow: currentOptions.EmailRequestsPerWindow,
				windowSeconds: currentOptions.EmailWindowSeconds,
				ct: cancellationToken
			);

			if (!emailAllowed)
				return TResponse.CreateFailure(error: new RateLimitExceededException(commandName: typeof(TRequest).Name));
		}

		return await next(t: cancellationToken);
	}
}