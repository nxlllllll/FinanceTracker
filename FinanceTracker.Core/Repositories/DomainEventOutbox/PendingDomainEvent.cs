namespace FinanceTracker.Core.Repositories.DomainEventOutbox;

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
