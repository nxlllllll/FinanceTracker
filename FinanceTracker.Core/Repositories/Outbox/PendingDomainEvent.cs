namespace FinanceTracker.Core.Repositories.Outbox;

public sealed record PendingDomainEvent(
	Guid Id,
	string EventType,
	Guid AggregateId,
	string AggregateType,
	Guid? CorrelationId,
	string Payload,
	DateTimeOffset OccurredAt,
	int RetryCount
);
