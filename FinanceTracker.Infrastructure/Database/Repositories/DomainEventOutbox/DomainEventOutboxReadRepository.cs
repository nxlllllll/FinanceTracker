using FinanceTracker.Core.Repositories.DomainEventOutbox;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.DomainEventOutbox;

public sealed class DomainEventOutboxReadRepository(
	FinanceTrackerContext context
) : IDomainEventOutboxReadRepository
{
	public async Task<IReadOnlyList<PendingDomainEvent>> GetPendingBatchAsync(
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.WithSkipLocked<DomainEventOutboxEntity>()
			.Where(predicate: e => e.ProcessedAt == null && e.FailedAt == null)
			.OrderBy(keySelector: e => e.CreatedAt)
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