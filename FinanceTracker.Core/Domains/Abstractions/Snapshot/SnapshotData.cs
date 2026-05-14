namespace FinanceTracker.Core.Domains.Abstractions.Snapshot;

public sealed record SnapshotData(
	Guid AggregateId,
	string AggregateType,
	int Version,
	string State
);