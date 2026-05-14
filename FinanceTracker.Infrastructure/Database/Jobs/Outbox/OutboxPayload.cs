namespace FinanceTracker.Infrastructure.Database.Jobs.Outbox;

public sealed record OutboxPayload(
	Guid AggregateId,
	Guid CorrelationId,
	IReadOnlyList<OutboxEventEnvelope> Events
);