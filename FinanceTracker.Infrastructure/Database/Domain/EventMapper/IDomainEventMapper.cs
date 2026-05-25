using FinanceTracker.Contracts.Events.Domain;
using FinanceTracker.Core.Domains.Abstractions.DomainEvent;

namespace FinanceTracker.Infrastructure.Database.Domain.EventMapper;

public interface IDomainEventMapper
{
	IDomainIntegrationEvent? Map(IDomainEvent @event);
}
