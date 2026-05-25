namespace FinanceTracker.Core.Repositories.DomainEventOutbox;

public interface IDomainEventOutboxReadRepository
{
	Task<IReadOnlyList<PendingDomainEvent>> GetPendingBatchAsync(
		int batchSize,
		CancellationToken ct = default
	);
}
