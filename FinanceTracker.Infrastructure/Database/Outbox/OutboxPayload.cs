namespace FinanceTracker.Infrastructure.Database.Outbox;

public sealed record OutboxPayload(
	Guid AggregateId,
	IReadOnlyList<OutboxEventEnvelope> Events
);