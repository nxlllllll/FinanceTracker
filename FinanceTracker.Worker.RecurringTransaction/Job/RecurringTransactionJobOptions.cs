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

	/// <summary>
	/// How far past its due instant an operation must be before the delay stops looking like ordinary
	/// polling lag and starts looking like the job having been down. Default: 24.
	/// </summary>
	[Range(minimum: 1, maximum: 168)]
	public int OverdueAfterHours { get; init; } = 24;

	[Required]
	public string CronExpression { get; init; } = "0 0 0/3 * * ?";

	[Required]
	public string Group { get; init; } = "default";

	[Required]
	public string TriggerName { get; init; } = "RecurringTransactionTrigger";
}
