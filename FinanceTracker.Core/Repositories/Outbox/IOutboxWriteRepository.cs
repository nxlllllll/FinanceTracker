namespace FinanceTracker.Core.Repositories.Outbox;

public interface IOutboxWriteRepository
{
	Task MarkAsPublishedAsync(
		Guid messageId,
		DateTimeOffset processedAt,
		CancellationToken ct = default
	);

	Task MarkAsPublishedBatchAsync(
		IReadOnlyCollection<Guid> messageIds,
		DateTimeOffset processedAt,
		CancellationToken ct = default
	);

	Task MarkAsFailedAsync(
		Guid messageId,
		int retryCount,
		DateTimeOffset? failedAt,
		CancellationToken ct = default
	);

	Task<int> DeleteProcessedAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default
	);

	Task<int> DeleteFailedAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default
	);
}
