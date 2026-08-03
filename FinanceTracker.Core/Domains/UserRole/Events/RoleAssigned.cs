using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.UserRole.Events;

[EventType(name: "userrole.role_assigned")]
public sealed record RoleAssigned(
	Guid Id,
	Guid UserId,
	Guid RoleId,
	Guid AssignedBy,
	int Version,
	DateTimeOffset OccurredAt
) : IEvent
{
	public IEvent WithVersion(int version) => this with { Version = version };
}
