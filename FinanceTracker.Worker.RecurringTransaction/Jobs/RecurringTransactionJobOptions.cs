using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.RecurringTransaction.Jobs;

public sealed class RecurringTransactionJobOptions
{
	public const string SectionName = "Jobs:RecurringTransaction";

	[Required]
	public string CronExpression { get; init; } = "0 0 3 * * ?";

	[Required]
	public string Group { get; init; } = "default";

	[Required]
	public string TriggerName { get; init; } = "RecurringTransactionTrigger";
}