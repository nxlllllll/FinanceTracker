using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.DeadLetterMonitor.Job;

public sealed class DeadLetterMonitoringOptions
{
	public const string SectionName = "DeadLetterMonitoring";

	public bool IsEnabled { get; init; } = true;

	[Range(minimum: 1, maximum: 1440)]
	public int IntervalMinutes { get; init; } = 5;

	[Required]
	public string Group { get; init; } = "default";

	[Required]
	public string TriggerName { get; init; } = "DeadLetterMonitoringTrigger";
}
