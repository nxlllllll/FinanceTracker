namespace FinanceTracker.Core.Repositories.Snapshot;

public interface ISnapshotWriteRepository
{
	Task<int> DeleteOldAsync(
		int batchSize,
		CancellationToken ct = default
	);
}
