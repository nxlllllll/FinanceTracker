using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.UserRole.Events;

namespace FinanceTracker.Contracts.Events.UserRole;

[IntegrationEventType(domainEventType: typeof(UserRoleCreated))]
public sealed record UserRoleCreatedEvent(
	Guid EventId,
	Guid UserId,
	int Version,
	DateTimeOffset OccurredAt
) : IIntegrationEvent
{
	Guid IIntegrationEvent.AggregateId => UserId;
}
