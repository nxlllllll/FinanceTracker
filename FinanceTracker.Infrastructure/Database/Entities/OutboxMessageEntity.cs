namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class OutboxMessageEntity
{
	public Guid Id { get; init; }
	public Guid AggregateId { get; init; }
	public string AggregateType { get; init; } = String.Empty;
	public string Payload { get; init; } = String.Empty;
	public DateTime CreatedAt { get; init; }
	public DateTime? ProcessedAt { get; set; }
}