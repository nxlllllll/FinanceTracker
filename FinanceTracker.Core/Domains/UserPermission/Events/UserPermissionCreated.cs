using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.UserPermission.Events;

[EventType(name: "userpermission.created")]
public sealed record UserPermissionCreated(
	Guid Id,
	Guid UserId,
	int Version,
	DateTimeOffset OccurredAt
) : IEvent
{
	public IEvent WithVersion(int version) => this with { Version = version };
}
