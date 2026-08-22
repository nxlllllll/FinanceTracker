using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.UserPermission.Events;

namespace FinanceTracker.Contracts.Events.UserPermission;

[IntegrationEventType(domainEventType: typeof(UserPermissionCreated))]
public sealed record UserPermissionCreatedEvent(
	Guid EventId,
	Guid UserId,
	int Version,
	DateTimeOffset OccurredAt
) : IIntegrationEvent
{
	Guid IIntegrationEvent.AggregateId => UserId;
}
