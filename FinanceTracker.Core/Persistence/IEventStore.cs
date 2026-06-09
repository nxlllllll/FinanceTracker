using FinanceTracker.Core.Domains.Abstractions.EventStore;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Persistence;

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
		string aggregateType,
		CancellationToken ct = default
	);

	IAsyncEnumerable<Guid> GetAggregateIdsAsync(
		string aggregateType,
		CancellationToken ct = default
	);
}