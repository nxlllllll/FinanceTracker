namespace FinanceTracker.Core.Domains.Abstractions;

public sealed record SnapshotData(
	Guid AggregateId,
	string AggregateType,
	int Version,
	string State
);