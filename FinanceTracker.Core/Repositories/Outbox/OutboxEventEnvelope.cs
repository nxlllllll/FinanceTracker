namespace FinanceTracker.Core.Repositories.Outbox;

public record OutboxEventEnvelope(
	string EventType,
	string EventPayload
);
