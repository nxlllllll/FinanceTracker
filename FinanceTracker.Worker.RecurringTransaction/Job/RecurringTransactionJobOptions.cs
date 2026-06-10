using System.ComponentModel.DataAnnotations;
using FinanceTracker.Worker.Shared.Job;

namespace FinanceTracker.Worker.RecurringTransaction.Job;

/// <summary>
/// Configuration for <see cref="RecurringTransactionHandlingJob"/>.
/// Bind from <c>appsettings.json</c> under the <c>"RecurringTransaction"</c> section.
/// </summary>
public sealed class RecurringTransactionJobOptions : IJobOptions
{
	public const string SectionName = "RecurringTransaction";

	/// <inheritdoc/>
	public bool IsEnabled { get; init; } = true;

	/// <summary>Quartz cron expression. Default: daily at 03:00.</summary>
	[Required]
	public string CronExpression { get; init; } = "0 0 3 * * ?";

	[Required]
	public string Group { get; init; } = "default";

	[Required]
	public string TriggerName { get; init; } = "RecurringTransactionTrigger";
}