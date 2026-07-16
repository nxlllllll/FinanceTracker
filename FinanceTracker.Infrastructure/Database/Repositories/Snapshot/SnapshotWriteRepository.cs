using FinanceTracker.Core.Repositories.Snapshot;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Snapshot;

public sealed class SnapshotWriteRepository(
	FinanceTrackerContext context
) : ISnapshotWriteRepository
{
	public async Task<int> DeleteOldAsync(
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.Database.ExecuteSqlRawAsync(sql: """
			DELETE FROM snapshots
			USING (
				SELECT aggregate_id, aggregate_type, version
				FROM (
					SELECT aggregate_id, aggregate_type, version, ROW_NUMBER() OVER (PARTITION BY aggregate_id, aggregate_type ORDER BY version DESC) AS rn
					FROM snapshots
				) t
				WHERE rn > 1
				LIMIT {0}
			) old
			WHERE snapshots.aggregate_id   = old.aggregate_id
			  AND snapshots.aggregate_type = old.aggregate_type
			  AND snapshots.version        = old.version
		""", parameters: [batchSize], cancellationToken: ct);
	}
}
