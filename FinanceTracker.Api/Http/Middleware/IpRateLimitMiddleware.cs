using System.Globalization;
using System.Net;
using System.Net.Mime;
using System.Net.Sockets;
using FinanceTracker.Api.Configurations;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Core.Services.RateLimit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Api.Http.Middleware;

/// <summary>Caps how much traffic one address may produce, ahead of authentication.</summary>
public sealed class IpRateLimitMiddleware(
	RequestDelegate next,
	IOptionsMonitor<IpRateLimitOptions> options,
	ILogger<IpRateLimitMiddleware> logger)
{
	private const string KeyPrefix = "ratelimit:ip:";

	public async Task InvokeAsync(
		HttpContext context,
		IRateLimiter rateLimiter,
		ICorrelationContext correlationContext)
	{
		IpRateLimitOptions current = options.CurrentValue;

		if (!current.Enabled)
		{
			await next(context: context);
			return;
		}

		string? partition = PartitionFor(address: context.Connection.RemoteIpAddress);

		if (partition is null)
		{
			logger.ZLogDebug(message: $"[IpRateLimit] No remote address on {context.Request.Path} — skipped.");
			await next(context: context);
			return;
		}

		RateLimitResult result = await rateLimiter.IsAllowedAsync(
			key: KeyPrefix + partition,
			requestsPerWindow: current.RequestsPerWindow,
			windowSeconds: current.WindowSeconds,
			ct: context.RequestAborted
		);

		if (result.IsAllowed)
		{
			await next(context: context);
			return;
		}

		logger.ZLogWarning(message: $"[IpRateLimit] {partition} exceeded {current.RequestsPerWindow} requests per {current.WindowSeconds}s on {context.Request.Path}.");

		await WriteRejectionAsync(
			context: context,
			retryAfterSeconds: result.RetryAfterSeconds,
			correlationContext: correlationContext
		);
	}

	/// <summary>
	/// Groups an address into the unit the limit is counted against, or <c>null</c> when there is no
	/// address to count.
	/// </summary>
	private static string? PartitionFor(IPAddress? address)
	{
		if (address is null)
			return null;

		if (address.IsIPv4MappedToIPv6)
			address = address.MapToIPv4();

		if (address.AddressFamily != AddressFamily.InterNetworkV6)
			return address.ToString();

		byte[] prefix = address.GetAddressBytes();
		Array.Clear(array: prefix, index: 8, length: 8);

		return new IPAddress(address: prefix) + "/64";
	}

	private static async Task WriteRejectionAsync(
		HttpContext context,
		int retryAfterSeconds,
		ICorrelationContext correlationContext)
	{
		context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
		context.Response.ContentType = MediaTypeNames.Application.ProblemJson;

		if (retryAfterSeconds > 0)
			context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(provider: CultureInfo.InvariantCulture);

		ProblemDetails problem = new ProblemDetails
		{
			Status = StatusCodes.Status429TooManyRequests,
			Title = "Too many requests",
			Detail = "Too many requests from this address. Please retry later.",
			Extensions =
			{
				["code"] = "rate_limit.ip_exceeded",
				["traceId"] = correlationContext.CorrelationId
			}
		};

		await context.Response.WriteAsJsonAsync(
			value: problem,
			options: null,
			contentType: MediaTypeNames.Application.ProblemJson,
			cancellationToken: CancellationToken.None
		);
	}
}

public static class IpRateLimitMiddlewareExtensions
{
	/// <summary>
	/// Registers the per-IP ceiling. Place it after forwarded headers have been applied, so the
	/// address counted is the client's rather than the proxy's, and before authentication, so token
	/// validation is covered by it.
	/// </summary>
	public static IApplicationBuilder UseIpRateLimit(this IApplicationBuilder app)
		=> app.UseMiddleware<IpRateLimitMiddleware>();
}
