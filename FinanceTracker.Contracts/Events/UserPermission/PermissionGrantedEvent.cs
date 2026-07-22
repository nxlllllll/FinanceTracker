using FinanceTracker.Contracts.Events.Abstraction;

namespace FinanceTracker.Contracts.Events.UserPermission;

[IntegrationEventType(domainEventType: typeof(Core.Domains.UserPermission.Events.PermissionGranted))]
public sealed record PermissionGrantedEvent(
	Guid EventId,
	Guid UserId,
	Guid GrantedBy,
	string Permission,
	int Version,
	DateTimeOffset OccurredAt
) : IIntegrationEvent
{
	Guid IIntegrationEvent.AggregateId => UserId;
}
