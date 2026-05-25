using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Behaviours.RateLimit;

public sealed class RateLimitOptions
{
	public const string SectionName = "RateLimit";

	[Range(minimum: 1, maximum: 10000)]
	public int RequestsPerWindow { get; init; } = 60;

	[Range(minimum: 1, maximum: 3600)]
	public int WindowSeconds { get; init; } = 60;
}
