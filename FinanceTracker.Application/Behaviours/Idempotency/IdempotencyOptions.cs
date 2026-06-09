using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Behaviours.Idempotency;

public sealed class IdempotencyOptions
{
	public const string SectionName = "Idempotency";

	[Range(minimum: 1, maximum: 720)]
	public int ExpiryHours { get; init; } = 24;

	[Required, Range(minimum: 5, maximum: 300)]
	public int InFlightInitialDelayMs { get; init; } = 50;

	[Required, Range(minimum: 100, maximum: 5000)]
	public int InFlightMaxDelayMs { get; init; } = 500;

	[Required, Range(minimum: 100, maximum: 30000)]
	public int InFlightMaxWaitMs { get; init; } = 1_000;

	[Required, Range(minimum: 5, maximum: 300)]
	public int AbandonedAfterSeconds { get; init; } = 5;

	public bool UseJitter { get; init; } = true;
}