namespace FinanceTracker.Infrastructure.Database.Context.ProcessedMessage;

public sealed class ProcessedMessageEntity
{
	public Guid MessageId { get; init; }
	public string ConsumerType { get; init; } = String.Empty;
	public DateTimeOffset ProcessedAt { get; init; }
}
