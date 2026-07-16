namespace FinanceTracker.Core.Repositories.ProcessedMessage;

public interface IProcessedMessageWriteRepository
{
	Task MarkAsProcessedAsync(
		Guid messageId,
		string consumerType,
		DateTimeOffset processedAt,
		CancellationToken ct = default
	);

	Task<int> DeleteOldAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default
	);
}
