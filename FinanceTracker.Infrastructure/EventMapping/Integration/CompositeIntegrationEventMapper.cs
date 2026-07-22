using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.EventMapping.Integration;

public sealed class CompositeIntegrationEventMapper(
	IEnumerable<IAggregateIntegrationEventMapper> mappers,
	ILogger<CompositeIntegrationEventMapper> logger
) : IIntegrationEventMapper
{
	public IIntegrationEvent? Map(IEvent @event)
	{
		foreach (IIntegrationEventMapper mapper in mappers)
		{
			IIntegrationEvent? mapped = mapper.Map(@event: @event);
			if (mapped is not null)
				return mapped;
		}

		logger.ZLogWarning(message:
			$"[IntegrationEventMapper] No integration event mapping defined for domain event '{@event.GetType().Name}' " +
			$"across {mappers.Count()} registered mapper(s). The event will not be published to the outbox."
		);

		return null;
	}
}
