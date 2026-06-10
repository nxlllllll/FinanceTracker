using System.ComponentModel.DataAnnotations;
using FinanceTracker.Worker.Shared.Job;

namespace FinanceTracker.Worker.Outbox.Job;

/// <summary>
/// Configuration for <see cref="OutboxPublisherJob"/>.
/// Bind from <c>appsettings.json</c> under the <c>"Outbox"</c> section.
/// </summary>
public sealed class OutboxOptions : IJobOptions
{
	public const string SectionName = "Outbox";

	/// <inheritdoc/>
	public bool IsEnabled { get; init; } = true;

	/// <summary>How often the job polls for pending outbox messages. Default: every 3 seconds.</summary>
	[Range(minimum: 1, maximum: 60)]
	public int IntervalSeconds { get; init; } = 3;

	/// <summary>Maximum messages processed per job execution. Default: 20.</summary>
	[Range(minimum: 1, maximum: 1000)]
	public int BatchSize { get; init; } = 20;

	/// <summary>
	/// Maximum publish attempts before a message is moved to <c>unresolvable_events</c>. Default: 5.
	/// </summary>
	[Range(minimum: 1, maximum: 100)]
	public int MaxRetries { get; init; } = 5;

	[Required]
	public string Group { get; init; } = "default";

	[Required]
	public string TriggerName { get; init; } = "OutboxWorkerTrigger";
}