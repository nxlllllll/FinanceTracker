using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.Cleanup.Job;

public sealed class CleanupOptions
{
	public const string SectionName = "Cleanup";

	public bool IsEnabled { get; init; } = true;

	public string Group { get; init; } = "cleanup";
	public string TriggerName { get; init; } = "cleanup-trigger";

	[Range(minimum: 1, maximum: 1440)]
	public int IntervalMinutes { get; init; } = 60;

	[Range(minimum: 1, maximum: 10_000)]
	public int BatchSize { get; init; } = 1000;

	[Range(minimum: 1, maximum: 365)]
	public int ProcessedMessageRetentionDays { get; init; } = 30;

	[Range(minimum: 1, maximum: 365)]
	public int OutboxProcessedRetentionDays { get; init; } = 7;

	[Range(minimum: 1, maximum: 365)]
	public int OutboxFailedRetentionDays { get; init; } = 30;
}
