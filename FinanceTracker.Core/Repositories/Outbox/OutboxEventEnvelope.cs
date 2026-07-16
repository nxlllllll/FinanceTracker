namespace FinanceTracker.Core.Repositories.Outbox;

/// <summary>
/// Wraps a single serialized integration event inside an <see cref="OutboxPayload"/>.
/// <param name="EventType">Integration event type discriminator (e.g. <c>"account.created"</c>).</param>
/// <param name="EventPayload">JSON-serialized integration event payload.</param>
/// </summary>
public record OutboxEventEnvelope(
	string EventType,
	string EventPayload
);
