using FinanceTracker.Contracts.Events.Abstraction;

namespace FinanceTracker.Contracts.Events.UserPermission;

[IntegrationEventType(domainEventType: typeof(Core.Domains.UserPermission.Events.UserPermissionCreated))]
public sealed record UserPermissionCreatedEvent(
	Guid EventId,
	Guid UserId,
	int Version,
	DateTimeOffset OccurredAt
) : IIntegrationEvent
{
	Guid IIntegrationEvent.AggregateId => UserId;
}
