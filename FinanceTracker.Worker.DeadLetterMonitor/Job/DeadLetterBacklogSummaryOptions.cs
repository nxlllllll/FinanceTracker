using System.ComponentModel.DataAnnotations;
using FinanceTracker.Worker.Shared.Job;

namespace FinanceTracker.Worker.DeadLetterMonitor.Job;

/// <summary>
/// Configuration for <see cref="DeadLetterBacklogSummaryJob"/>.
/// Bind from <c>appsettings.json</c> under the <c>"DeadLetterBacklogSummary"</c> section.
/// </summary>
public sealed class DeadLetterBacklogSummaryOptions : IJobOptions
{
	public const string SectionName = "DeadLetterBacklogSummary";

	/// <inheritdoc/>
	public bool IsEnabled { get; init; } = true;

	/// <summary>How often the summary check runs. Default: once a day.</summary>
	[Range(minimum: 1, maximum: 10080)]
	public int IntervalMinutes { get; init; } = 1440;

	/// <summary>An unresolved event is only included in the summary once it's older than this.</summary>
	[Range(minimum: 1, maximum: 720)]
	public int UnresolvedOlderThanHours { get; init; } = 24;

	/// <summary>Maximum number of individual events listed in the summary log, oldest first.</summary>
	[Range(minimum: 1, maximum: 200)]
	public int SampleSize { get; init; } = 20;

	[Required]
	public string Group { get; init; } = "default";

	[Required]
	public string TriggerName { get; init; } = "DeadLetterBacklogSummaryTrigger";
}