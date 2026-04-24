namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class SnapshotEntity
{
	public Guid AggregateId { get; set; }
	public string AggregateType { get; set; } = String.Empty;
	public int Version { get; set; }
	public string State { get; set; } = String.Empty;
	public DateTime CreatedAt { get; set; }
}