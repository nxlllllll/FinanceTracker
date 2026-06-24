namespace FinanceTracker.Infrastructure.Database.Context.Outbox;

public sealed class OutboxMessageEntity
{
	public Guid Id { get; init; }
	public Guid AggregateId { get; init; }
	public string AggregateType { get; init; } = String.Empty;
	public string Payload { get; init; } = String.Empty;
	public DateTimeOffset UpdatedAt { get; init; }
	public DateTimeOffset? ProcessedAt { get; init; }
	public int RetryCount { get; init; }
	public DateTimeOffset? FailedAt { get; init; }
	public DateTimeOffset? LockedUntil { get; init; }
}