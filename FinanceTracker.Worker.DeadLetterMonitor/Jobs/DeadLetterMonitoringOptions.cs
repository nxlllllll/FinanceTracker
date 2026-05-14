using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.DeadLetterMonitor.Jobs;

public sealed class DeadLetterMonitoringOptions
{
	public const string SectionName = "Jobs:DeadLetterMonitoring";

	[Range(minimum: 1, maximum: 1440)]
	public int IntervalMinutes { get; init; } = 5;

	[Required]
	public string Group { get; init; } = "default";

	[Required]
	public string TriggerName { get; init; } = "DeadLetterMonitoringTrigger";
}