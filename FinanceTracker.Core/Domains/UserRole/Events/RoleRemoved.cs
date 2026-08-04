using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.UserRole.Events;

[EventType(name: "userrole.role_removed")]
public sealed record RoleRemoved(
	Guid Id,
	Guid UserId,
	Guid RoleId,
	Guid RemovedBy,
	int Version,
	DateTimeOffset OccurredAt
) : IEvent
{
	public IEvent WithVersion(int version) => this with { Version = version };
}
