namespace FinanceTracker.Infrastructure.Database.Context.EventStore;

public sealed class EventEntity
{
	public Guid Id { get; init; }
	public Guid AggregateId { get; init; }
	public string AggregateType { get; init; } = String.Empty;
	public string EventType { get; init; } = String.Empty;
	public int Version { get; init; }
	public string Payload { get; init; } = String.Empty;
	public Guid CorrelationId { get; init; }
	public DateTimeOffset OccurredAt { get; init; }
	public DateTimeOffset CreatedAt { get; init; }
	public int SchemaVersion { get; init; }
}
