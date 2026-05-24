using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.ES.Event;

namespace FinanceTracker.Infrastructure.Database.EventStore.EventMapper;

public interface IIntegrationEventMapper
{
	IAccountIntegrationEvent? Map(IEvent @event);
}