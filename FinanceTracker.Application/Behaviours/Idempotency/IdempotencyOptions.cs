using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Behaviours.Idempotency;

public sealed class IdempotencyOptions
{
	public const string SectionName = "Idempotency";

	[Range(minimum: 1, maximum: 720)]
	public int ExpiryHours { get; init; } = 24;
}
