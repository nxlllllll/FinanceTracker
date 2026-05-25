namespace FinanceTracker.Infrastructure.Database.Jobs.Outbox;

public record OutboxEventEnvelope(
	string EventType,
	string EventPayload
);
