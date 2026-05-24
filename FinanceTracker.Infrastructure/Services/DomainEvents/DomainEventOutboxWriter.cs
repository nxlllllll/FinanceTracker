using System.Text.Json;
using FinanceTracker.Contracts.Events.Domain;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.DomainEvent;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.DomainEvents;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Domain.EventMapper;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;

namespace FinanceTracker.Infrastructure.Services.DomainEvents;

public sealed class DomainEventOutboxWriter(
	FinanceTrackerContext context,
	IEnumerable<IDomainEventMapper> mappers,
	IIntegrationEventTypeResolver integrationEventTypeResolver,
	IDateProvider dateProvider
) : IDomainEventOutboxWriter
{
	public async Task WriteAsync(
		IHasDomainEvents entity,
		Guid correlationId,
		CancellationToken ct = default)
	{
		if (entity.DomainEvents.Count == 0)
			return;

		DateTime now = dateProvider.UtcNow;

		foreach (IDomainEvent domainEvent in entity.DomainEvents)
		{
			if (!TryMap(domainEvent: domainEvent, out IDomainIntegrationEvent? integrationEvent))
				continue;

			string eventType = integrationEventTypeResolver.ResolveTypeName(eventType: integrationEvent!.GetType());
			string payload = JsonSerializer.Serialize(
				value: integrationEvent,
				inputType: integrationEvent.GetType(),
				options: FinanceTrackerJsonOptions.Payload
			);

			await context.DomainEventOutbox.AddAsync(entity: new DomainEventOutboxEntity()
			{
				Id = Guid.CreateVersion7(),
				EventType = eventType,
				AggregateId = domainEvent.AggregateId,
				AggregateType = entity.AggregateType,
				CorrelationId = correlationId,
				Payload = payload,
				OccurredAt = domainEvent.OccurredAt,
				CreatedAt = now
			}, cancellationToken: ct);
		}

		await context.SaveChangesAsync(cancellationToken: ct);
		entity.ClearDomainEvents();
	}

	private bool TryMap(IDomainEvent domainEvent, out IDomainIntegrationEvent? integrationEvent)
	{
		integrationEvent = null;
		foreach (IDomainEventMapper mapper in mappers)
		{
			integrationEvent = mapper.Map(@event: domainEvent);
			if (integrationEvent is not null)
				return true;
		}
		return false;
	}
}