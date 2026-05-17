using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.BalanceAdjustment.Jobs;

public sealed class BalanceAdjustmentJobOptions
{
	public const string SectionName = "BalanceAdjustmentJob";

	public string Group { get; init; } = "BalanceAdjustment";
	public string TriggerName { get; init; } = "BalanceAdjustmentTrigger";

	[Required]
	public string CronExpression { get; init; } = "0 30 2 * * ?";

	[Range(minimum: 1, maximum: 10)]
	public int MaxRetries { get; init; } = 3;

	[Range(minimum: 5, maximum: 5000)]
	public int BaseDelayMs { get; init; } = 20;

	public bool UseJitter { get; init; } = true;
}