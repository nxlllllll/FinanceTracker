using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.ReadModels.UnresolvableEvent;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Results;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.UnresolvableEvent;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.UnresolvableEvent;

public sealed class UnresolvableEventReadRepository(FinanceTrackerContext context) : IUnresolvableEventReadRepository
{
	public async Task<UnresolvableEventDetail?> GetByIdAsync(
		Guid eventId,
		CancellationToken ct = default)
	{
		return await context.UnresolvableEvents.AsNoTracking().Where(predicate: e => e.Id == eventId)
			.Select(selector: e => new UnresolvableEventDetail(
				Id: e.Id,
				Type: e.Type,
				ReferenceId: e.ReferenceId,
				Reason: e.Reason,
				Payload: e.Payload,
				OccurredAt: e.OccurredAt,
				AcknowledgedAt: e.AcknowledgedAt,
				ResolvedAt: e.ResolvedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<PagedResult<Core.ReadModels.UnresolvableEvent.UnresolvableEvent>> GetAllAsync(
		UnresolvableEventType? type = null,
		bool? isAcknowledged = null,
		bool? isResolved = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default)
	{
		IQueryable<UnresolvableEventEntity> query = context.UnresolvableEvents.AsNoTracking();

		if (type is not null)
			query = query.Where(predicate: e => e.Type == type);

		if (isAcknowledged is not null)
			query = isAcknowledged.Value
				? query.Where(predicate: e => e.AcknowledgedAt != null)
				: query.Where(predicate: e => e.AcknowledgedAt == null);

		if (isResolved is not null)
			query = isResolved.Value
				? query.Where(predicate: e => e.ResolvedAt != null)
				: query.Where(predicate: e => e.ResolvedAt == null);

		if (cursorOccurredAt is not null && cursorId is not null)
			query = query.Where(predicate: e => e.OccurredAt > cursorOccurredAt || e.OccurredAt == cursorOccurredAt && e.Id > cursorId);

		List<Core.ReadModels.UnresolvableEvent.UnresolvableEvent> items = await query
			.OrderBy(keySelector: e => e.OccurredAt)
			.ThenBy(keySelector: e => e.Id)
			.Take(count: pageSize + 1)
			.Select(selector: e => new Core.ReadModels.UnresolvableEvent.UnresolvableEvent(
				Id: e.Id,
				Type: e.Type,
				ReferenceId: e.ReferenceId,
				Reason: e.Reason,
				OccurredAt: e.OccurredAt,
				AcknowledgedAt: e.AcknowledgedAt,
				ResolvedAt: e.ResolvedAt
			)).ToListAsync(cancellationToken: ct);

		bool hasNextPage = items.Count > pageSize;
		if (hasNextPage)
			items.RemoveAt(index: items.Count - 1);

		Core.ReadModels.UnresolvableEvent.UnresolvableEvent? last = items.Count > 0 ? items[^1] : null;

		return new PagedResult<Core.ReadModels.UnresolvableEvent.UnresolvableEvent>(
			Items: items.AsReadOnly(),
			HasNextPage: hasNextPage,
			NextCursorDate: hasNextPage ? last?.OccurredAt : null,
			NextCursorId: hasNextPage ? last?.Id : null
		);
	}

	public async Task<UnresolvedBacklogSummary> GetUnresolvedOlderThanAsync(
		DateTimeOffset cutoff,
		int sampleSize,
		CancellationToken ct = default)
	{
		IQueryable<UnresolvableEventEntity> unresolved = context.UnresolvableEvents.AsNoTracking()
															.Where(predicate: e => e.ResolvedAt == null && e.OccurredAt < cutoff);

		int totalCount = await unresolved.CountAsync(cancellationToken: ct);

		if (totalCount == 0)
			return new UnresolvedBacklogSummary(TotalCount: 0, OldestOccurredAt: null, Sample: []);

		List<Core.ReadModels.UnresolvableEvent.UnresolvableEvent> sample = await unresolved.OrderBy(keySelector: e => e.OccurredAt)
			.Take(count: sampleSize)
			.Select(selector: e => new Core.ReadModels.UnresolvableEvent.UnresolvableEvent(
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

	public Task<int> CountUnresolvedAsync(CancellationToken ct = default)
	{
		return context.UnresolvableEvents.AsNoTracking().CountAsync(
			predicate: e => e.ResolvedAt == null,
			cancellationToken: ct
		);
	}
}
