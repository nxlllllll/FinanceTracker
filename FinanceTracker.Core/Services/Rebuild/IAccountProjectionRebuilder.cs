namespace FinanceTracker.Core.Services.Rebuild;

/// <summary>
/// Rebuilds the Account read-model projection from the event store.
/// Used after data migrations, projection bugs, or infrastructure incidents
/// that leave the read-model out of sync with the event log.
/// </summary>
public interface IAccountProjectionRebuilder
{
	/// <summary>
	/// Rebuilds the projection for a single account by replaying its events
	/// (or restoring from snapshot then replaying subsequent events).
	/// Deletes the existing projection row before inserting to avoid concurrency conflicts.
	/// </summary>
	Task RebuildAsync(Guid accountId, CancellationToken ct = default);

	/// <summary>
	/// Rebuilds projections for all accounts, processing them in parallel batches.
	/// Failures in individual accounts are logged and skipped — the job continues.
	/// </summary>
	Task RebuildAllAsync(int batchSize = 50, CancellationToken ct = default);
}
