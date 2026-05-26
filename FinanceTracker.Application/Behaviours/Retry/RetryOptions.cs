using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Behaviours.Retry;

public sealed class RetryOptions
{
	public const string SectionName = "Retry";

	[Range(minimum: 1, maximum: 10)]
	public int MaxRetries { get; init; } = 3;

	[Range(minimum: 5, maximum: 5000)]
	public int BaseDelayMs { get; init; } = 20;

	public bool UseJitter { get; init; } = true;
}
