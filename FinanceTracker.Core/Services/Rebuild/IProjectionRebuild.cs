using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Services.Rebuild;

public interface IProjectionRebuild
{
	/// <summary>
	/// Removes everything the projection holds for one aggregate, so the replay below starts from nothing.
	/// </summary>
	Task ClearAsync(Guid aggregateId, CancellationToken ct = default);

	/// <summary>
	/// Applies one domain event, read from the log rather than received from the broker.
	/// </summary>
	Task ApplyAsync(IEvent @event, CancellationToken ct = default);
}
