using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.UserPermission.Events;

[EventType(name: "userpermission.permission_revoked")]
public sealed record PermissionRevoked(
	Guid Id,
	Guid UserId,
	Guid RevokedBy,
	string Permission,
	int Version,
	DateTimeOffset OccurredAt
) : IEvent
{
	public IEvent WithVersion(int version) => this with { Version = version };
}
