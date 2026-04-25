using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Repositories;

public interface IEventStore
{
	Task SaveAsync(
		Guid aggregateId,
		string aggregateType,
		IEnumerable<IEvent> events,
		int expectedVersion,
		Func<string>? snapshotFactory = null,
		CancellationToken ct = default
	);

	Task<EventStoreResult> LoadAsync(
		Guid aggregateId,
		CancellationToken ct = default
	);
}