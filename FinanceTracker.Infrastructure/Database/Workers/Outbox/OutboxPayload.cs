namespace FinanceTracker.Infrastructure.Database.Workers.Outbox;

public sealed record OutboxPayload(
	Guid AggregateId,
	IReadOnlyList<OutboxEventEnvelope> Events
);