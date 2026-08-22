using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.UserPermission.Events;

namespace FinanceTracker.Contracts.Events.UserPermission;

[IntegrationEventType(domainEventType: typeof(PermissionRevoked))]
public sealed record PermissionRevokedEvent(
	Guid EventId,
	Guid UserId,
	Guid RevokedBy,
	string Permission,
	int Version,
	DateTimeOffset OccurredAt
) : IIntegrationEvent
{
	Guid IIntegrationEvent.AggregateId => UserId;
}
