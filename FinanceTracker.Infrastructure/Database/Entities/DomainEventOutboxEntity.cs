namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class DomainEventOutboxEntity
{
	public Guid Id { get; init; }
	public string EventType { get; init; } = String.Empty;
	public Guid AggregateId { get; init; }
	public string AggregateType { get; init; } = String.Empty;
	public Guid? CorrelationId { get; init; }
	public string Payload { get; init; } = String.Empty;
	public DateTimeOffset OccurredAt { get; init; }
	public DateTimeOffset CreatedAt { get; init; }
	public DateTimeOffset? ProcessedAt { get; set; }
	public int RetryCount { get; set; }
	public DateTimeOffset? FailedAt { get; set; }
}
