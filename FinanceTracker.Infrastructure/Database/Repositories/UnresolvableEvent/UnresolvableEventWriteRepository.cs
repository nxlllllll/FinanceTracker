using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.UnresolvableEvent;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.UnresolvableEvent;

public sealed class UnresolvableEventWriteRepository(FinanceTrackerContext context) : IUnresolvableEventWriteRepository
{
	public async Task CreateAsync(
		UnresolvableEventType type,
		Guid referenceId,
		string reason,
		string payload,
		DateTimeOffset occurredAt,
		CancellationToken ct = default)
	{
		await context.UnresolvableEvents.AddAsync(entity: new UnresolvableEventEntity
		{
			Id = Guid.CreateVersion7(),
			Type = type,
			ReferenceId = referenceId,
			Reason = reason,
			Payload = payload,
			OccurredAt = occurredAt,
			AcknowledgedAt = null,
			ResolvedAt = null
		}, cancellationToken: ct);
	}

	public async Task AcknowledgeBatchAsync(
		IReadOnlyList<Guid> ids,
		DateTimeOffset acknowledgedAt,
		CancellationToken ct = default)
	{
		await context.UnresolvableEvents.Where(predicate: e => ids.Contains(e.Id)).ExecuteUpdateAsync(
			setPropertyCalls: e => e.SetProperty(propertyExpression: x => x.AcknowledgedAt, valueExpression: acknowledgedAt),
			cancellationToken: ct
		);
	}

	public async Task ResolveAsync(
		Guid id,
		DateTimeOffset resolvedAt,
		CancellationToken ct = default)
	{
		await context.UnresolvableEvents.Where(predicate: e => e.Id == id).ExecuteUpdateAsync(
			setPropertyCalls: e => e.SetProperty(propertyExpression: x => x.ResolvedAt, valueExpression: resolvedAt),
			cancellationToken: ct
		);
	}
}
