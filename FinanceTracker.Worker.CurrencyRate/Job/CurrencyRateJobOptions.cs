using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.CurrencyRate.Job;

public sealed class CurrencyRateJobOptions
{
	public const string SectionName = "CurrencyRateJob";

	public bool IsEnabled { get; init; } = true;

	public string Group { get; init; } = "CurrencyRate";
	public string TriggerName { get; init; } = "CurrencyRateTrigger";

	[Required]
	public string CronExpression { get; init; } = "0 0 2 * * ?";
}
