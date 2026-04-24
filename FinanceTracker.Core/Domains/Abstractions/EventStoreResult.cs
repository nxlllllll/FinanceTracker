namespace FinanceTracker.Core.Domains.Abstractions;

public sealed record EventStoreResult(
	SnapshotData? Snapshot,
	IReadOnlyList<IEvent> Events
);