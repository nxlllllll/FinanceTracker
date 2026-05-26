using FinanceTracker.Contracts.Events.Domain;
using FinanceTracker.Core.Domains.Abstractions.DomainEvent;

namespace FinanceTracker.Infrastructure.EventMapping.Domain;

public interface IDomainEventMapper
{
	IDomainIntegrationEvent? Map(IDomainEvent @event);
}
