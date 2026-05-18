namespace FinanceTracker.Core.Repositories.Outbox;

public interface IOutboxWriteRepository
{
	Task MarkAsPublishedAsync(
		Guid messageId,
		DateTime processedAt,
		CancellationToken ct = default
	);

	Task MarkAsFailedAsync(
		Guid messageId,
		int retryCount,
		DateTime? failedAt,
		CancellationToken ct = default
	);

	Task<int> DeleteProcessedAsync(
		DateTime before,
		int batchSize,
		CancellationToken ct = default
	);

	Task<int> DeleteFailedAsync(
		DateTime before,
		int batchSize,
		CancellationToken ct = default
	);
}