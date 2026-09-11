using System.ComponentModel.DataAnnotations;
using FinanceTracker.Worker.Shared.Job;

namespace FinanceTracker.Worker.DeadLetterMonitor.Job;

/// <summary>
/// Configuration for <see cref="UnresolvableEventGaugeJob"/>.
/// Bind from <c>appsettings.json</c> under the <c>"UnresolvableEventGauge"</c> section.
/// </summary>
public sealed class UnresolvableEventGaugeOptions : IJobOptions
{
	public const string SectionName = "UnresolvableEventGauge";

	/// <inheritdoc/>
	public bool IsEnabled { get; init; } = true;

	/// <summary>How often the pending count is republished. Default: every 5 minutes.</summary>
	[Range(minimum: 1, maximum: 1440)]
	public int IntervalMinutes { get; init; } = 5;

	[Required]
	public string Group { get; init; } = "default";

	[Required]
	public string TriggerName { get; init; } = "UnresolvableEventGaugeTrigger";
}
