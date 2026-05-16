namespace FinanceTracker.Core.Repositories.Outbox;

public sealed record DeadLetterMessage(
	Guid Id,
	Guid AggregateId,
	string AggregateType,
	int RetryCount,
	DateTime FailedAt
);