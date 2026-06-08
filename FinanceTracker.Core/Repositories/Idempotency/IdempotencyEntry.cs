namespace FinanceTracker.Core.Repositories.Idempotency;

public sealed record IdempotencyEntry(
	string? ResponseJson,
	DateTimeOffset ReservedAt
);