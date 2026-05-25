namespace FinanceTracker.Core.Repositories.DomainEventOutbox;

public interface IDomainEventOutboxWriteRepository
{
	Task MarkAsProcessedAsync(
		Guid id,
		DateTimeOffset processedAt, 
		CancellationToken ct = default
	);

	Task MarkAsFailedAsync(
		Guid id,
		int retryCount, DateTimeOffset? failedAt, 
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
