using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Configurations.Options;

/// <summary>
/// Bind from <c>appsettings.json</c> under the <c>"CurrencyRate"</c> section.
/// </summary>
public sealed class CurrencyRateOptions
{
	public const string SectionName = "CurrencyRate";

	/// <summary>
	/// How stale a fallback rate may be before a converted total is refused outright rather than
	/// reported as approximate. Default: 2 days.
	/// </summary>
	[Range(minimum: 1, maximum: 365)]
	public int MaxStalenessDays { get; init; } = 2;
}
