using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.BalanceAdjustment.Jobs;

public sealed class BalanceAdjustmentJobOptions
{
	public const string SectionName = "BalanceAdjustmentJob";

	public string Group { get; init; } = "BalanceAdjustment";
	public string TriggerName { get; init; } = "BalanceAdjustmentTrigger";

	[Required]
	public string CronExpression { get; init; } = "0 30 2 * * ?";
}