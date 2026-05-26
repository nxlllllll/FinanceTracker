namespace FinanceTracker.Core.Repositories.Outbox;

public sealed record OutboxPayload(
	Guid AggregateId,
	Guid CorrelationId,
	IReadOnlyList<OutboxEventEnvelope> Events
);
