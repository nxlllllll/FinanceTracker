using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Contracts.Events.UserRole;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.UserRole.Events;

namespace FinanceTracker.Infrastructure.EventMapping.Integration;

public sealed class UserRoleIntegrationEventMapper : IAggregateIntegrationEventMapper
{
	public IIntegrationEvent? Map(IEvent @event) => @event switch
	{
		UserRoleCreated e => new UserRoleCreatedEvent(
			EventId: e.Id,
			UserId: e.UserId,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		RoleAssigned e => new RoleAssignedEvent(
			EventId: e.Id,
			UserId: e.UserId,
			RoleId: e.RoleId,
			AssignedBy: e.AssignedBy,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		RoleRemoved e => new RoleRemovedEvent(
			EventId: e.Id,
			UserId: e.UserId,
			RoleId: e.RoleId,
			RemovedBy: e.RemovedBy,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		_ => null
	};
}
