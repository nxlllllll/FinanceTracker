namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class DomainEventOutboxEntity
{
	public Guid Id { get; init; }
	public string EventType { get; init; } = String.Empty;
	public Guid AggregateId { get; init; }
	public string AggregateType { get; init; } = String.Empty;
	public Guid? CorrelationId { get; init; }
	public string Payload { get; init; } = String.Empty;
	public DateTime OccurredAt { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? ProcessedAt { get; set; }
	public int RetryCount { get; set; }
	public DateTime? FailedAt { get; set; }
}