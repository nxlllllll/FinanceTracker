namespace FinanceTracker.Core.Domains.Abstractions.Snapshot;

public interface ISnapshotSerializer<TAggregate>
{
	string Serialize(TAggregate aggregate);
	TAggregate Deserialize(SnapshotData snapshot);
}