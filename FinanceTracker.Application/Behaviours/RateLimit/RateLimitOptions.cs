using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Behaviours.RateLimit;

/// <summary>
/// Configuration for <c>RateLimitingBehavior</c>.
/// Defines the sliding-window rate limit applied to all <c>IUserScopedRequest</c> commands.
/// Bind from <c>appsettings.json</c> under the <c>"RateLimit"</c> section.
/// </summary>
public sealed class RateLimitOptions
{
	public const string SectionName = "RateLimit";

	/// <summary>Maximum number of requests allowed per user within the window. Default: 60.</summary>
	[Range(minimum: 1, maximum: 10000)]
	public int RequestsPerWindow { get; init; } = 60;

	/// <summary>Duration of the sliding window in seconds. Default: 60.</summary>
	[Range(minimum: 1, maximum: 3600)]
	public int WindowSeconds { get; init; } = 60;
}
