namespace FinanceTracker.Core.Domains.Abstractions.Snapshot;

/// <summary>
/// Serializes and deserializes an aggregate root to and from a snapshot string.
/// Used by the event store to persist periodic aggregate state checkpoints,
/// reducing the number of events that must be replayed on load.
/// </summary>
public interface ISnapshotSerializer<TAggregate>
{
	/// <summary>Serializes the aggregate state to a string for storage.</summary>
	string Serialize(TAggregate aggregate);

	/// <summary>
	/// Reconstructs an aggregate instance from the given snapshot data.
	/// The snapshot's <c>State</c> string must have been produced by <see cref="Serialize"/>.
	/// </summary>
	TAggregate Deserialize(SnapshotData snapshot);
}
