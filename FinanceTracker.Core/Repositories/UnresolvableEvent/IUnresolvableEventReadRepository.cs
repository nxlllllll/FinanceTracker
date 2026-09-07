using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.ReadModels.UnresolvableEvent;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Repositories.UnresolvableEvent;

public interface IUnresolvableEventReadRepository : IReadRepository<ReadModels.UnresolvableEvent.UnresolvableEvent>
{
	/// <summary>
	/// Returns one event together with the message body that caused it.
	/// </summary>
	Task<UnresolvableEventDetail?> GetByIdAsync(
		Guid eventId,
		CancellationToken ct = default
	);

	/// <summary>Pages the escalation queue oldest first</summary>
	Task<PagedResult<ReadModels.UnresolvableEvent.UnresolvableEvent>> GetAllAsync(
		UnresolvableEventType? type = null,
		bool? isAcknowledged = null,
		bool? isResolved = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
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
