namespace FinanceTracker.Core.Services.RateLimit;

/// <summary>
/// Sliding-window rate limiter backed by Redis.
/// Each call atomically records the request and checks whether the limit has been exceeded.
/// </summary>
public interface IRateLimiter
{
	/// <summary>
	/// Checks whether the request is within the allowed rate, admitting it if so.
	/// </summary>
	/// <param name="key">Unique key identifying the subject being rate-limited (e.g. user ID + endpoint).</param>
	/// <param name="requestsPerWindow">Maximum number of requests allowed within the window.</param>
	/// <param name="windowSeconds">Duration of the sliding window in seconds.</param>
	Task<RateLimitResult> IsAllowedAsync(
		string key,
		int requestsPerWindow,
		int windowSeconds,
		CancellationToken ct = default
	);
}
