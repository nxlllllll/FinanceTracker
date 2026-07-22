using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Contracts.Events.UserPermission;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.UserPermission.Events;

namespace FinanceTracker.Infrastructure.EventMapping.Integration;

public sealed class UserPermissionIntegrationEventMapper : IAggregateIntegrationEventMapper
{
	public IIntegrationEvent? Map(IEvent @event) => @event switch
	{
		UserPermissionCreated e => new UserPermissionCreatedEvent(
			EventId: e.Id,
			UserId: e.UserId,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		PermissionGranted e => new PermissionGrantedEvent(
			EventId: e.Id,
			UserId: e.UserId,
			GrantedBy: e.GrantedBy,
			Permission: e.Permission,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		PermissionRevoked e => new PermissionRevokedEvent(
			EventId: e.Id,
			UserId: e.UserId,
			RevokedBy: e.RevokedBy,
			Permission: e.Permission,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		_ => null
	};
}
