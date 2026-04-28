namespace FinanceTracker.Infrastructure.Database.Workers.Outbox;

public record OutboxEventEnvelope(
	string EventType,
	string EventPayload
);