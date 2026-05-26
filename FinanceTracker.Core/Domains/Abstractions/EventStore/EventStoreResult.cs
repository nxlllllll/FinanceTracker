using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.Snapshot;

namespace FinanceTracker.Core.Domains.Abstractions.EventStore;

public sealed record EventStoreResult(
	SnapshotData? Snapshot,
	IReadOnlyList<IEvent> Events
);
