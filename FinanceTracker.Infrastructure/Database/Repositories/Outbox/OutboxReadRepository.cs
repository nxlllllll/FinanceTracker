using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Outbox;

public sealed class OutboxReadRepository(FinanceTrackerContext context) : IOutboxReadRepository
{
	public async Task<IReadOnlyList<PendingOutboxMessage>> GetPendingBatchAsync(
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.OutboxMessages.AsNoTracking().Where(predicate: m => m.ProcessedAt == null && m.FailedAt == null)
			.OrderBy(keySelector: m => m.UpdatedAt)
			.Take(count: batchSize)
			.Select(selector: m => new PendingOutboxMessage(
				Id: m.Id,
				AggregateId: m.AggregateId,
				AggregateType: m.AggregateType,
				Payload: m.Payload,
				RetryCount: m.RetryCount
			)).ToListAsync(cancellationToken: ct);
	}
}