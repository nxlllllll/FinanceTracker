using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.Account.Events;


[EventType(name: "account.unarchived")]
public sealed record AccountUnarchived(
	Guid Id,
	Guid AccountId,
	DateTimeOffset OccurredAt
) : IEvent;
