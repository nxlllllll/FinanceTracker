using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.RecurringTransaction.Job;

public sealed class RecurringTransactionJobOptions
{
	public const string SectionName = "RecurringTransaction";

	public bool IsEnabled { get; init; } = true;

	[Required]
	public string CronExpression { get; init; } = "0 0 3 * * ?";

	[Required]
	public string Group { get; init; } = "default";

	[Required]
	public string TriggerName { get; init; } = "RecurringTransactionTrigger";
}
