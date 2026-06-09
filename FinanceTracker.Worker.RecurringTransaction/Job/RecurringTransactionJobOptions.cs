using System.ComponentModel.DataAnnotations;
using FinanceTracker.Worker.Shared.Job;

namespace FinanceTracker.Worker.RecurringTransaction.Job;

public sealed class RecurringTransactionJobOptions : IJobOptions
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