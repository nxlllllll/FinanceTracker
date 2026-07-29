using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Results;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.UnresolvableEvent;

public sealed class UnresolvableEventReadRepository(FinanceTrackerContext context) : IUnresolvableEventReadRepository
{
	public async Task<PagedResult<Core.ReadModels.UnresolvableEvent>> GetUnacknowledgedBatchAsync(
			int batchSize,
			CancellationToken ct = default)
	{
		List<Core.ReadModels.UnresolvableEvent> items = await context.UnresolvableEvents.AsNoTracking()
			.Where(predicate: e => e.AcknowledgedAt == null && e.ResolvedAt == null)
			.OrderBy(keySelector: e => e.OccurredAt)
			.Take(count: batchSize + 1)
			.Select(selector: e => new Core.ReadModels.UnresolvableEvent(
				Id: e.Id,
				Type: e.Type,
				ReferenceId: e.ReferenceId,
				Reason: e.Reason,
				OccurredAt: e.OccurredAt,
				AcknowledgedAt: e.AcknowledgedAt,
				ResolvedAt: e.ResolvedAt
			)).ToListAsync(cancellationToken: ct);

		bool hasNextPage = items.Count > batchSize;
		if (hasNextPage)
			items.RemoveAt(index: items.Count - 1);

		return new PagedResult<Core.ReadModels.UnresolvableEvent>(
			Items: items.AsReadOnly(),
			HasNextPage: hasNextPage,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	public async Task<UnresolvedBacklogSummary> GetUnresolvedOlderThanAsync(
		DateTimeOffset cutoff,
		int sampleSize,
		CancellationToken ct = default)
	{
		IQueryable<Context.UnresolvableEvent.UnresolvableEventEntity> unresolved = context.UnresolvableEvents.AsNoTracking()
			.Where(predicate: e => e.ResolvedAt == null && e.OccurredAt < cutoff);

		int totalCount = await unresolved.CountAsync(cancellationToken: ct);

		if (totalCount == 0)
			return new UnresolvedBacklogSummary(TotalCount: 0, OldestOccurredAt: null, Sample: []);

		List<Core.ReadModels.UnresolvableEvent> sample = await unresolved.OrderBy(keySelector: e => e.OccurredAt)
			.Take(count: sampleSize)
			.Select(selector: e => new Core.ReadModels.UnresolvableEvent(
				Id: e.Id,
				Type: e.Type,
				ReferenceId: e.ReferenceId,
				Reason: e.Reason,
				OccurredAt: e.OccurredAt,
				AcknowledgedAt: e.AcknowledgedAt,
				ResolvedAt: e.ResolvedAt
			)).ToListAsync(cancellationToken: ct);

		return new UnresolvedBacklogSummary(
			TotalCount: totalCount,
			OldestOccurredAt: sample[0].OccurredAt,
			Sample: sample
		);
	}
}
