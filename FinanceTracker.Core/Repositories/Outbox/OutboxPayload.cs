namespace FinanceTracker.Core.Repositories.Outbox;

/// <summary>
/// The payload stored in the outbox table for a single aggregate write.
/// Deserialized by <c>OutboxPublisherJob</c> before constructing the RabbitMQ message.
/// </summary>
public sealed record OutboxPayload(
	Guid AggregateId,
	Guid CorrelationId,
	IReadOnlyList<OutboxEventEnvelope> Events,
	string? TraceParent = null,
	string? TraceState = null
);
