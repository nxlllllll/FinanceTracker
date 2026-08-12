using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.UserRole.Events;

namespace FinanceTracker.Contracts.Events.UserRole;

[IntegrationEventType(domainEventType: typeof(RoleRemoved))]
public sealed record RoleRemovedEvent(
	Guid EventId,
	Guid UserId,
	Guid RoleId,
	Guid RemovedBy,
	int Version,
	DateTimeOffset OccurredAt
) : IIntegrationEvent
{
	Guid IIntegrationEvent.AggregateId => UserId;
}
