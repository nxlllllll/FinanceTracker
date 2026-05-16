namespace FinanceTracker.Core.Repositories.Outbox;

public sealed record PendingOutboxMessage(
	Guid Id,
	Guid AggregateId,
	string AggregateType,
	string Payload,
	int RetryCount
);