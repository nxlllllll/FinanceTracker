using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.UserRole.Events;

[EventType(name: "userrole.created")]
public sealed record UserRoleCreated(
	Guid Id,
	Guid UserId,
	int Version,
	DateTimeOffset OccurredAt
) : IEvent
{
	public IEvent WithVersion(int version) => this with { Version = version };
}
