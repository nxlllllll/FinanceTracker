using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Outbox;

public sealed class OutboxReadRepository(FinanceTrackerContext context) : IOutboxReadRepository
{
	/// <summary>
	/// Atomically selects up to <paramref name="batchSize"/> unprocessed messages and marks
	/// them as claimed by setting <c>locked_until</c>, in a single SQL statement.
	/// </summary>
	public async Task<IReadOnlyList<PendingOutboxMessage>> ClaimPendingBatchAsync(
		int batchSize,
		DateTimeOffset now,
		TimeSpan leaseDuration,
		CancellationToken ct = default)
	{
		DateTimeOffset lockedUntil = now + leaseDuration;

		return await context.Database.SqlQuery<PendingOutboxMessage>(sql: $"""
			WITH claimed AS (
				SELECT id
				FROM outbox_messages
				WHERE processed_at IS NULL
				  AND failed_at IS NULL
				  AND (locked_until IS NULL OR locked_until < {now})
				ORDER BY updated_at
				LIMIT {batchSize}
				FOR UPDATE SKIP LOCKED
			)
			UPDATE outbox_messages o
			SET locked_until = {lockedUntil}
			FROM claimed c
			WHERE o.id = c.id
			RETURNING o.id AS "Id", o.aggregate_id AS "AggregateId", o.aggregate_type AS "AggregateType", o.payload AS "Payload", o.retry_count AS "RetryCount"
		""").ToListAsync(cancellationToken: ct);
	}
}