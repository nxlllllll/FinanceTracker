using FinanceTracker.Core.Domains.Abstractions.ES.Event;
using FinanceTracker.Core.Domains.Abstractions.Snapshot;

namespace FinanceTracker.Core.Domains.Abstractions.ES;

public sealed record EventStoreResult(
	SnapshotData? Snapshot,
	IReadOnlyList<IEvent> Events
);