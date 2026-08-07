using System.ComponentModel.DataAnnotations;
using FinanceTracker.Worker.Shared.Job;

namespace FinanceTracker.Worker.BaseCurrencyRecalculation.Job;

/// <summary>
/// Configuration for <see cref="BaseCurrencyRecalculationJob"/>.
/// Bind from <c>appsettings.json</c> under the <c>"BaseCurrencyRecalculationJob"</c> section.
/// </summary>
public sealed class BaseCurrencyRecalculationJobOptions : IJobOptions
{
	public const string SectionName = "BaseCurrencyRecalculationJob";

	/// <inheritdoc/>
	public bool IsEnabled { get; init; } = true;

	public string Group { get; init; } = "BaseCurrencyRecalculation";
	public string TriggerName { get; init; } = "BaseCurrencyRecalculationTrigger";

	/// <summary>Quartz cron expression. Default: every minute.</summary>
	[Required]
	public string CronExpression { get; init; } = "0 * * * * ?";

	/// <summary>How many users to rebuild per run.</summary>
	[Range(minimum: 1, maximum: 1_000)]
	public int BatchSize { get; init; } = 10;

	/// <summary>
	/// How long a claimed rebuild stays claimed before another worker may take it. Default: 15 minutes.
	/// </summary>
	[Range(minimum: 1, maximum: 120)]
	public int LeaseMinutes { get; init; } = 15;

	/// <summary>
	/// Failed attempts before a rebuild is abandoned. Default: 5.
	/// </summary>
	[Range(minimum: 1, maximum: 20)]
	public int MaxAttempts { get; init; } = 5;
}
