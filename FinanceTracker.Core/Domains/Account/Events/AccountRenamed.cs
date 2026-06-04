using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Account.Events;

[EventType(name: "account.renamed")]
public sealed record AccountRenamed(
	Guid Id,
	Guid AccountId,
	Name NewName,
	int Version,
	DateTimeOffset OccurredAt
) : IEvent
{
	public IEvent WithVersion(int version) => this with { Version = version };
}