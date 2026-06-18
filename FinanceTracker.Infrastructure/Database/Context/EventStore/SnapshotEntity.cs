namespace FinanceTracker.Infrastructure.Database.Context.EventStore;

public sealed class SnapshotEntity
{
	public Guid AggregateId { get; init; }
	public string AggregateType { get; init; } = String.Empty;
	public int Version { get; init; }
	public string State { get; init; } = String.Empty;
	public DateTimeOffset CreatedAt { get; init; }
}