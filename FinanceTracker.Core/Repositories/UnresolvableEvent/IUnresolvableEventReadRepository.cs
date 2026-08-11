using FinanceTracker.Core.ReadModels.UnresolvableEvent;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Repositories.UnresolvableEvent;

public interface IUnresolvableEventReadRepository : IReadRepository<ReadModels.UnresolvableEvent.UnresolvableEvent>
{
	/// <summary>
	/// Returns events that haven't been individually reported yet (and aren't already resolved) —
	/// used by <c>DeadLetterMonitoringJob</c>'s frequent pass (every few minutes).
	/// </summary>
	Task<PagedResult<ReadModels.UnresolvableEvent.UnresolvableEvent>> GetUnacknowledgedBatchAsync(
		int batchSize,
		CancellationToken ct = default
	);

	/// <summary>
	/// Returns a count and a capped sample of still-unresolved events older than <paramref name="cutoff"/>,
	/// regardless of whether they were already acknowledged — used by <c>DeadLetterBacklogSummaryJob</c>'s
	/// infrequent (daily) pass
	/// </summary>
	Task<UnresolvedBacklogSummary> GetUnresolvedOlderThanAsync(
		DateTimeOffset cutoff,
		int sampleSize,
		CancellationToken ct = default
	);

	/// <summary>
	/// Counts every event still awaiting human resolution, whether it has been acknowledged.
	/// </summary>
	Task<int> CountUnresolvedAsync(CancellationToken ct = default);
}
