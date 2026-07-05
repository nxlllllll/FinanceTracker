using System.Diagnostics.Metrics;

namespace FinanceTracker.Core.Services.Metrics;

/// <summary>
/// Central OpenTelemetry metrics for cross-cutting application concerns, shared across
/// Core, Application, and Infrastructure. Mirrors <c>FinanceTrackerActivitySource</c>.
/// Register via <c>AddMeter("FinanceTracker")</c> in your OTEL configuration.
/// </summary>
public static class FinanceTrackerMetrics
{
	public const string MeterName = "FinanceTracker";

	private static readonly Meter Meter = new Meter(name: MeterName);

	/// <summary>
	/// Incremented every time <c>FallbackRateLimiter</c> falls back to the in-memory limiter
	/// because the Redis-backed limiter was unavailable or too slow to respond.
	/// A sustained non-zero rate indicates Redis is down or unreachable and should alert.
	/// </summary>
	public static readonly Counter<long> RateLimiterFallbackActivated = Meter.CreateCounter<long>(
		name: "ratelimiter.fallback.activated",
		description: "Total number of requests where the rate limiter fell back to in-memory because Redis was unavailable."
	);
}
