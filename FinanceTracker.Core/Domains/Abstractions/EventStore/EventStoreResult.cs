using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.Snapshot;

namespace FinanceTracker.Core.Domains.Abstractions.EventStore;

/// <summary>
/// The result of loading an aggregate from the event store.
/// Contains the latest snapshot (if one exists) and all events appended after it.
/// If no snapshot exists, <see cref="Snapshot"/> is <c>null</c> and
/// <see cref="Events"/> contains the full event history.
/// <param name="Snapshot">
/// The latest snapshot for this aggregate, or <c>null</c>
/// if no snapshot has been taken.
/// </param>
/// <param name="Events">
/// Events appended after the snapshot version, in ascending version order.
/// Empty when the snapshot is fully up-to-date.
/// </param>
/// </summary>
public sealed record EventStoreResult(
	SnapshotData? Snapshot,
	IReadOnlyList<IEvent> Events
);
