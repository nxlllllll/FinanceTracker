using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Behaviours.Idempotency;

/// <summary>
/// Configuration for <c>IdempotencyBehavior</c>.
/// Bind from <c>appsettings.json</c> under the <c>"Idempotency"</c> section.
/// </summary>
public sealed class IdempotencyOptions
{
	public const string SectionName = "Idempotency";

	/// <summary>How long a completed idempotency record is retained before cleanup. Default: 24 hours.</summary>
	[Range(minimum: 1, maximum: 720)]
	public int ExpiryHours { get; init; } = 24;

	/// <summary>Initial polling delay when waiting for an in-flight duplicate to complete. Default: 50ms.</summary>
	[Required, Range(minimum: 5, maximum: 300)]
	public int InFlightInitialDelayMs { get; init; } = 50;

	/// <summary>Maximum polling delay cap (exponential backoff ceiling). Default: 500ms.</summary>
	[Required, Range(minimum: 100, maximum: 5000)]
	public int InFlightMaxDelayMs { get; init; } = 500;

	/// <summary>Total time to wait for an in-flight duplicate before returning a timeout error. Default: 1000ms.</summary>
	[Required, Range(minimum: 100, maximum: 30000)]
	public int InFlightMaxWaitMs { get; init; } = 1_000;

	/// <summary>
	/// Age in seconds after which an in-flight record is considered abandoned
	/// (i.e. the original handler crashed). The record is deleted and the client
	/// receives an error suggesting a retry. Default: 5s.
	/// </summary>
	[Required, Range(minimum: 5, maximum: 300)]
	public int AbandonedAfterSeconds { get; init; } = 5;

	/// <summary>
	/// When <c>true</c>, applies full jitter to polling delays via <c>RetryDelayCalculator</c>
	/// to prevent thundering herd when many concurrent requests poll the same key. Default: <c>true</c>.
	/// </summary>
	public bool UseJitter { get; init; } = true;
}
