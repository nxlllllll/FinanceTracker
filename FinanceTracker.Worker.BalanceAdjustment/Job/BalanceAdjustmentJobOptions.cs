using System.ComponentModel.DataAnnotations;
using FinanceTracker.Worker.Shared.Job;

namespace FinanceTracker.Worker.BalanceAdjustment.Job;

/// <summary>
/// Configuration for <see cref="BalanceAdjustmentJob"/>.
/// Bind from <c>appsettings.json</c> under the <c>"BalanceAdjustmentJob"</c> section.
/// </summary>
public sealed class BalanceAdjustmentJobOptions : IJobOptions
{
	public const string SectionName = "BalanceAdjustmentJob";

	/// <inheritdoc/>
	public bool IsEnabled { get; init; } = true;

	public string Group { get; init; } = "BalanceAdjustment";
	public string TriggerName { get; init; } = "BalanceAdjustmentTrigger";

	/// <summary>Quartz cron expression. Default: daily at 02:30.</summary>
	[Required]
	public string CronExpression { get; init; } = "0 30 2 * * ?";

	/// <summary>Maximum retries on concurrency conflict per item. Default: 3.</summary>
	[Range(minimum: 1, maximum: 10)]
	public int MaxRetries { get; init; } = 3;

	/// <summary>Base delay in milliseconds for exponential backoff on retry. Default: 20ms.</summary>
	[Range(minimum: 5, maximum: 5000)]
	public int BaseDelayMs { get; init; } = 20;

	/// <summary>When <c>true</c>, applies jitter to retry delays. Default: <c>true</c>.</summary>
	public bool UseJitter { get; init; } = true;

	/// <summary>Maximum number of pending-rate rows fetched per page.</summary>
	[Range(minimum: 1, maximum: 10_000)]
	public int BatchSize { get; init; } = 500;
}
