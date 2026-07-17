using System.Net;
using Microsoft.Extensions.Primitives;

namespace FinanceTracker.Api.Infrastructure;

public static class HttpContextExtensions
{
	private const string IdempotencyKeyHeader = "Idempotency-Key";

	/// <summary>
	/// Reads the mandatory <c>Idempotency-Key</c> header. Returns <c>null</c> when the header
	/// is missing or not a valid GUID — the endpoint must respond 400 in that case.
	/// </summary>
	public static Guid? GetIdempotencyKey(this HttpContext httpContext)
	{
		if (!httpContext.Request.Headers.TryGetValue(key: IdempotencyKeyHeader, value: out StringValues raw))
			return null;

		return Guid.TryParse(input: raw.ToString(), result: out Guid key) ? key : null;
	}

	/// <summary>Client IP for ip-scoped rate limiting.</summary>
	public static IPAddress GetClientIpAddress(this HttpContext httpContext)
		=> httpContext.Connection.RemoteIpAddress ?? IPAddress.None;
}
