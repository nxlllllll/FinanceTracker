namespace FinanceTracker.Core.Repositories.Outbox;

public interface IOutboxReadRepository
{
	Task<IReadOnlyList<PendingOutboxMessage>> ClaimPendingBatchAsync(
		int batchSize,
		DateTimeOffset now,
		TimeSpan leaseDuration,
		CancellationToken ct = default
	);
}