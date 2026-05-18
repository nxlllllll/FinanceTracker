namespace FinanceTracker.Core.Repositories.ProcessedMessage;

public interface IProcessedMessageWriteRepository
{
	Task MarkAsProcessedAsync(
		Guid messageId,
		string consumerType,
		DateTime processedAt,
		CancellationToken ct = default
	);

	Task<int> DeleteOldAsync(
		DateTime before,
		int batchSize,
		CancellationToken ct = default
	);
}