using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Account.Events;

[EventType(name: "account.created")]
public sealed record AccountCreated(
	Guid Id,
	Guid AccountId,
	Guid UserId,
	Name Name,
	AccountType Type,
	Currency Currency,
	decimal Balance,
	int Version,
	DateTimeOffset OccurredAt
) : IEvent
{
	public IEvent WithVersion(int version) => this with { Version = version };
}