using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;

namespace FinanceTracker.Infrastructure.Database.Repositories.UnresolvableEvent;

public sealed class UnresolvableEventWriteRepository(
	FinanceTrackerContext context
) : IUnresolvableEventWriteRepository
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
			OccurredAt = occurredAt
		}, cancellationToken: ct);

		await context.SaveChangesAsync(cancellationToken: ct);
	}
}
