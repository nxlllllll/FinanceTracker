using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.UserPermission.Events;

[EventType(name: "userpermission.permission_granted")]
public sealed record PermissionGranted(
	Guid Id,
	Guid UserId,
	Guid GrantedBy,
	string Permission,
	int Version,
	DateTimeOffset OccurredAt
) : IEvent
{
	public IEvent WithVersion(int version) => this with { Version = version };
}
