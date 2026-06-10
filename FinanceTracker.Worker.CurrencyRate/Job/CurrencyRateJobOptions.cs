using System.ComponentModel.DataAnnotations;
using FinanceTracker.Worker.Shared.Job;

namespace FinanceTracker.Worker.CurrencyRate.Job;

/// <summary>
/// Configuration for <see cref="CurrencyRateJob"/>.
/// Bind from <c>appsettings.json</c> under the <c>"CurrencyRateJob"</c> section.
/// </summary>
public sealed class CurrencyRateJobOptions : IJobOptions
{
	public const string SectionName = "CurrencyRateJob";

	/// <inheritdoc/>
	public bool IsEnabled { get; init; } = true;

	public string Group { get; init; } = "CurrencyRate";
	public string TriggerName { get; init; } = "CurrencyRateTrigger";

	/// <summary>Quartz cron expression. Default: daily at 02:00.</summary>
	[Required]
	public string CronExpression { get; init; } = "0 0 2 * * ?";
}