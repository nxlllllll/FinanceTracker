namespace FinanceTracker.Infrastructure.Database.Context.EventStore;

public sealed class SnapshotEntity
{
	public Guid AggregateId { get; set; }
	public string AggregateType { get; set; } = String.Empty;
	public int Version { get; set; }
	public string State { get; set; } = String.Empty;
	public DateTimeOffset CreatedAt { get; set; }
}
