namespace FinanceTracker.Core.Repositories.DomainEventOutbox;

public interface IDomainEventOutboxWriteRepository
{
	Task MarkAsProcessedAsync(
		Guid id,
		DateTime processedAt, 
		CancellationToken ct = default
	);

	Task MarkAsFailedAsync(
		Guid id,
		int retryCount, DateTime? failedAt, 
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