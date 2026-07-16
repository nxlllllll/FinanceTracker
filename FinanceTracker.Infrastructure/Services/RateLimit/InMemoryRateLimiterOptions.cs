using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Services.RateLimit;

/// <summary>
/// Configuration for <see cref="InMemoryRateLimiter"/>.
/// Bind from <c>appsettings.json</c> under the <c>"InMemoryRateLimiter"</c> section.
/// </summary>
public sealed class InMemoryRateLimiterOptions
{
	public const string SectionName = "InMemoryRateLimiter";

	/// <summary>Upper bound on the number of distinct keys tracked at once.</summary>
	[Range(minimum: 1_000, maximum: 10_000_000)]
	public int MaxTrackedKeys { get; init; } = 100_000;

	/// <summary>
	/// How many <see cref="InMemoryRateLimiter.IsAllowedAsync"/> calls occur
	/// between opportunistic sweeps that drop keys nobody has touched recently
	/// </summary>
	[Range(minimum: 10, maximum: 1_000_000)]
	public int SweepIntervalCalls { get; init; } = 1_000;

	/// <summary>How long a key can go untouched before the sweep reclaims it.</summary>
	[Range(minimum: 60_000, maximum: 86_400_000)]
	public int KeyIdleTimeoutMs { get; init; } = 900_000;
}
