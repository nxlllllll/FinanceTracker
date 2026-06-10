using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Behaviours.Retry;

/// <summary>
/// Configuration for <c>ConcurrencyRetryBehavior</c>.
/// Bind from <c>appsettings.json</c> under the <c>"Retry"</c> section.
/// </summary>
public sealed class RetryOptions
{
	public const string SectionName = "Retry";

	/// <summary>Maximum number of retry attempts on concurrency conflict. Default: 3.</summary>
	[Range(minimum: 1, maximum: 10)]
	public int MaxRetries { get; init; } = 3;

	/// <summary>Base delay in milliseconds for exponential backoff. Default: 20ms.</summary>
	[Range(minimum: 5, maximum: 5000)]
	public int BaseDelayMs { get; init; } = 20;

	/// <summary>
	/// When <c>true</c>, applies full jitter to retry delays to spread concurrent retries.
	/// Default: <c>true</c>.
	/// </summary>
	public bool UseJitter { get; init; } = true;
}