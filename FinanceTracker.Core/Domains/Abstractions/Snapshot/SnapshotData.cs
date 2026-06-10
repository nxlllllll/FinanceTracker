namespace FinanceTracker.Core.Domains.Abstractions.Snapshot;

/// <summary>
/// Raw snapshot data as loaded from the event store.
/// Passed to <see cref="ISnapshotSerializer{TAggregate}.Deserialize"/> to reconstruct
/// the aggregate at the snapshot's version before replaying subsequent events.
/// <param name="AggregateId">ID of the aggregate this snapshot belongs to.</param>
/// <param name="AggregateType">Aggregate type discriminator (e.g. <c>"Account"</c>).</param>
/// <param name="Version">Aggregate version at the time the snapshot was taken.</param>
/// <param name="State">Serialized aggregate state produced by <see cref="ISnapshotSerializer{TAggregate}.Serialize"/>.</param>
/// </summary>
public sealed record SnapshotData(
	Guid AggregateId,
	string AggregateType,
	int Version,
	string State
);