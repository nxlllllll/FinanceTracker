using FinanceTracker.Core.Domains.Abstractions.EventStore;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Persistence;

/// <summary>
/// Append-only store for domain events. Provides optimistic concurrency via
/// <paramref name="expectedVersion"/> and optional snapshot support.
/// </summary>
public interface IEventStore
{
	/// <summary>
	/// Appends events for the given aggregate and optionally writes a snapshot.
	/// Throws <c>ConcurrencyConflictException</c> if the current version in the store
	/// does not match <paramref name="expectedVersion"/>.
	/// </summary>
	Task SaveAsync(
		Guid aggregateId,
		string aggregateType,
		IEnumerable<IEvent> events,
		int expectedVersion,
		Func<string>? snapshotFactory = null,
		CancellationToken ct = default
	);

	/// <summary>
	/// Loads the latest snapshot (if any) and all events appended after it.
	/// Returns an empty result if the aggregate does not exist.
	/// </summary>
	Task<EventStoreResult> LoadAsync(
		Guid aggregateId,
		string aggregateType,
		CancellationToken ct = default
	);

	/// <summary>
	/// Loads every event for the aggregate, from the first one, ignoring snapshots.
	/// </summary>
	Task<IReadOnlyList<IEvent>> LoadAllEventsAsync(
		Guid aggregateId,
		string aggregateType,
		CancellationToken ct = default
	);

	/// <summary>
	/// Streams distinct aggregate IDs of the given type without loading all into memory.
	/// Intended for bulk operations such as projection rebuilds.
	/// </summary>
	IAsyncEnumerable<Guid> GetAggregateIdsAsync(
		string aggregateType,
		CancellationToken ct = default
	);
}
