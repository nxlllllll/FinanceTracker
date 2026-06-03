namespace FinanceTracker.Core.Repositories.Outbox;

public interface IOutboxReadRepository
{
	Task<IReadOnlyList<PendingOutboxMessage>> GetPendingBatchAsync(
		int batchSize,
		CancellationToken ct = default
	);
}
