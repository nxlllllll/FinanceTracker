using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.TransferProjection.Job;

public sealed class TransferCreditLagOptions
{
	public const string SectionName = "TransferCreditLag";

	public bool IsEnabled { get; init; } = true;

	public string Group { get; init; } = "transfer-projection";
	public string TriggerName { get; init; } = "transfer-credit-lag-trigger";

	[Range(minimum: 1, maximum: 1440)]
	public int IntervalMinutes { get; init; } = 5;

	[Range(minimum: 1, maximum: 60)]
	public int GracePeriodMinutes { get; init; } = 5;
}
