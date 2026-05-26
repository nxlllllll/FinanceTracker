namespace FinanceTracker.Core.Repositories.Outbox;

public interface IDomainOutboxReadRepository
{
	Task<IReadOnlyList<PendingDomainEvent>> GetPendingBatchAsync(
		int batchSize,
		CancellationToken ct = default
	);
}
