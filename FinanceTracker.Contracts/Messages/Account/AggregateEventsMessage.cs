namespace FinanceTracker.Contracts.Messages.Account;

public sealed record AggregateEventsMessage(
	Guid MessageId,
	Guid AggregateId,
	string AggregateType,
	Guid CorrelationId,
	IReadOnlyList<EventEnvelope> Events
);