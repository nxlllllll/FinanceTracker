using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.Account.Events;

[EventType(name: "account.archived")]
public sealed record AccountArchived(
	Guid Id,
	Guid AccountId,
	int Version,
	DateTimeOffset OccurredAt
) : IEvent
{
	public IEvent WithVersion(int version) => this with { Version = version };
}