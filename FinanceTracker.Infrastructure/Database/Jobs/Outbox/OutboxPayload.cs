namespace FinanceTracker.Infrastructure.Database.Jobs.Outbox;

public sealed record OutboxPayload(
	Guid AggregateId,
	IReadOnlyList<OutboxEventEnvelope> Events
);