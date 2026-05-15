namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class ProcessedMessageEntity
{
	public Guid MessageId { get; init; }
	public string ConsumerType { get; init; } = String.Empty;
	public DateTime ProcessedAt { get; init; }
}