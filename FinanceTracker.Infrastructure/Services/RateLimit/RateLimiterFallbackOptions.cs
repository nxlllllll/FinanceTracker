using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Services.RateLimit;

/// <summary>
/// Configuration for <see cref="FallbackRateLimiter"/>.
/// Bind from <c>appsettings.json</c> under the <c>"RateLimiterFallback"</c> section.
/// </summary>
public sealed class RateLimiterFallbackOptions
{
	public const string SectionName = "RateLimiterFallback";

	/// <summary>
	/// Maximum time to wait for the Redis-backed limiter to respond before treating it as
	/// unavailable and falling back to the in-memory limiter for this request. A short bound
	/// keeps a slow-but-technically-up Redis from stalling every request in the auth pipeline.
	/// Default: 100ms.
	/// </summary>
	[Range(minimum: 10, maximum: 5000)]
	public int ProbeTimeoutMs { get; init; } = 100;

	/// <summary>
	/// How long to keep serving from the in-memory limiter before spending another probe on Redis.
	/// Default: 5 seconds.
	/// </summary>
	[Range(minimum: 100, maximum: 60_000)]
	public int RecoveryProbeIntervalMs { get; init; } = 5_000;
}

