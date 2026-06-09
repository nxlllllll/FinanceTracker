using System.ComponentModel.DataAnnotations;
using FinanceTracker.Worker.Shared.Job;

namespace FinanceTracker.Worker.Outbox.Job;

public sealed class OutboxOptions : IJobOptions
{
	public const string SectionName = "Outbox";

	public bool IsEnabled { get; init; } = true;

	[Range(minimum: 1, maximum: 60)]
	public int IntervalSeconds { get; init; } = 3;

	[Range(minimum: 1, maximum: 1000)]
	public int BatchSize { get; init; } = 20;

	[Range(minimum: 1, maximum: 100)]
	public int MaxRetries { get; init; } = 5;

	[Required]
	public string Group { get; init; } = "default";

	[Required]
	public string TriggerName { get; init; } = "OutboxWorkerTrigger";
}