using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Infrastructure.EventMapping.Integration;

public interface IIntegrationEventMapper
{
	IAccountIntegrationEvent? Map(IEvent @event);
}
