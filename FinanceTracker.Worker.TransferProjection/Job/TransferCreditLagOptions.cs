using System.ComponentModel.DataAnnotations;
using FinanceTracker.Worker.Shared.Job;

namespace FinanceTracker.Worker.TransferProjection.Job;

/// <summary>
/// Configuration for <see cref="TransferCreditLagJob"/>.
/// Bind from <c>appsettings.json</c> under the <c>"TransferCreditLag"</c> section.
/// </summary>
public sealed class TransferCreditLagOptions : IJobOptions
{
	public const string SectionName = "TransferCreditLag";

	/// <inheritdoc/>
	public bool IsEnabled { get; init; } = true;

	public string Group { get; init; } = "transfer-projection";
	public string TriggerName { get; init; } = "transfer-credit-lag-trigger";

	/// <summary>How often the job checks for pending credit transfers. Default: every 5 minutes.</summary>
	[Range(minimum: 1, maximum: 1440)]
	public int IntervalMinutes { get; init; } = 5;

	/// <summary>
	/// Minimum time since debit before a transfer is considered lagging.
	/// Allows normal processing time before raising an alert. Default: 5 minutes.
	/// </summary>
	[Range(minimum: 1, maximum: 60)]
	public int GracePeriodMinutes { get; init; } = 5;

	/// <summary>
	/// Minimum time since debit before a stuck transfer is automatically compensated.
	/// Must be greater than <see cref="GracePeriodMinutes"/> to avoid compensating
	/// transfers still in normal processing. Default: 30 minutes.
	/// </summary>
	[Range(minimum: 1, maximum: 1440)]
	public int CompensationThresholdMinutes { get; init; } = 30;
}