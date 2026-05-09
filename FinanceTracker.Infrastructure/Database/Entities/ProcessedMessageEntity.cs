namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class ProcessedMessageEntity
{
	public Guid MessageId { get; init; }
	public DateTime ProcessedAt { get; init; }
}