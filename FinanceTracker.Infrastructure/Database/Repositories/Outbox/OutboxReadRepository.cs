using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Outbox;

public sealed class OutboxReadRepository(
	FinanceTrackerContext context
) : IOutboxReadRepository
{
	public async Task<IReadOnlyList<PendingOutboxMessage>> GetPendingBatchAsync(
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.GetPendingOutboxBatch(batchSize: batchSize).Select(selector: m => new PendingOutboxMessage(
			Id: m.Id,
			AggregateId: m.AggregateId,
			AggregateType: m.AggregateType,
			Payload: m.Payload,
			RetryCount: m.RetryCount
		)).ToListAsync(cancellationToken: ct);
	}

	public async Task<IReadOnlyList<DeadLetterMessage>> GetDeadLettersAsync(
		CancellationToken ct = default)
	{
		return await context.OutboxMessages.AsNoTracking().Where(predicate: m => m.FailedAt != null)
			.OrderBy(keySelector: m => m.FailedAt)
			.Select(selector: m => new DeadLetterMessage(
				Id: m.Id,
				AggregateId: m.AggregateId,
				AggregateType: m.AggregateType,
				RetryCount: m.RetryCount,
				FailedAt: m.FailedAt!.Value
			)).ToListAsync(cancellationToken: ct);
	}
}