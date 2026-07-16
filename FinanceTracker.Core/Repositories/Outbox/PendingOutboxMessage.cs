namespace FinanceTracker.Core.Repositories.Outbox;

/// <summary>
/// Represents an outbox message that has not yet been successfully published to RabbitMQ.
/// Read by <c>OutboxPublisherJob</c> for batch processing.
/// </summary>
public sealed record PendingOutboxMessage(
	Guid Id,
	Guid AggregateId,
	string AggregateType,
	string Payload,
	int RetryCount
);
