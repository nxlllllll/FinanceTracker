namespace FinanceTracker.Infrastructure.Database.Context.Outbox;

public sealed class OutboxMessageEntity
{
	public Guid Id { get; init; }
	public Guid AggregateId { get; init; }
	public string AggregateType { get; init; } = String.Empty;
	public string Payload { get; init; } = String.Empty;
	public DateTimeOffset UpdatedAt { get; init; }
	public DateTimeOffset? ProcessedAt { get; set; }
	public int RetryCount { get; set; }
	public DateTimeOffset? FailedAt { get; set; }
}
