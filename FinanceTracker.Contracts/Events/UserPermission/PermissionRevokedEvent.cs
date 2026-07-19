using FinanceTracker.Contracts.Events.Abstraction;

namespace FinanceTracker.Contracts.Events.UserPermission;

[IntegrationEventType(domainEventType: typeof(Core.Domains.UserPermission.Events.PermissionRevoked))]
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
