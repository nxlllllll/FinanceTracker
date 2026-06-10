using System.ComponentModel.DataAnnotations;
using FinanceTracker.Worker.Shared.Job;

namespace FinanceTracker.Worker.DeadLetterMonitor.Job;

/// <summary>
/// Configuration for <see cref="DeadLetterMonitoringJob"/>.
/// Bind from <c>appsettings.json</c> under the <c>"DeadLetterMonitoring"</c> section.
/// </summary>
public sealed class DeadLetterMonitoringOptions : IJobOptions
{
	public const string SectionName = "DeadLetterMonitoring";

	/// <inheritdoc/>
	public bool IsEnabled { get; init; } = true;

	/// <summary>How often the job checks for unresolvable events. Default: every 5 minutes.</summary>
	[Range(minimum: 1, maximum: 1440)]
	public int IntervalMinutes { get; init; } = 5;

	[Required]
	public string Group { get; init; } = "default";

	[Required]
	public string TriggerName { get; init; } = "DeadLetterMonitoringTrigger";
}