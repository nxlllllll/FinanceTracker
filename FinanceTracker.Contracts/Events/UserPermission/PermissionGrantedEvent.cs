using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.UserPermission.Events;

namespace FinanceTracker.Contracts.Events.UserPermission;

[IntegrationEventType(domainEventType: typeof(PermissionGranted))]
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
