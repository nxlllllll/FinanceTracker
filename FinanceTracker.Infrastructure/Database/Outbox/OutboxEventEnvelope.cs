namespace FinanceTracker.Infrastructure.Database.Outbox;

public record OutboxEventEnvelope(
	string EventType,
	string EventPayload
);