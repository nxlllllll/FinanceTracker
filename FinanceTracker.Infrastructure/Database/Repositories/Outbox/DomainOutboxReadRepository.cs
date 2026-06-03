using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Outbox;

public sealed class DomainOutboxReadRepository(FinanceTrackerContext context) : IDomainOutboxReadRepository
{
	public async Task<IReadOnlyList<PendingDomainEvent>> GetPendingBatchAsync(
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.DomainEventOutbox.AsNoTracking().Where(predicate: o => o.ProcessedAt == null && o.FailedAt == null)
			.OrderBy(keySelector: o => o.CreatedAt)
			.Take(count: batchSize)
			.Select(selector: e => new PendingDomainEvent(
				Id: e.Id,
				EventType: e.EventType,
				AggregateId: e.AggregateId,
				AggregateType: e.AggregateType,
				CorrelationId: e.CorrelationId,
				Payload: e.Payload,
				OccurredAt: e.OccurredAt,
				RetryCount: e.RetryCount
			)).ToListAsync(cancellationToken: ct);
	}
}