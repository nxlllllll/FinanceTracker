using FinanceTracker.Core.Domains.Abstractions.ES.Event;

namespace FinanceTracker.Core.Domains.Account.Events;


[EventType(name: "account.unarchived")]
public sealed record AccountUnarchived(
	Guid Id,
	Guid AccountId,
	DateTime OccurredAt
) : IEvent;