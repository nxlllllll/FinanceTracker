using FinanceTracker.Core.Domains.Abstractions.ES.Event;

namespace FinanceTracker.Core.Domains.Account.Events;

[EventType(name: "account.archived")]
public sealed record AccountArchived(
	Guid Id,
	Guid AccountId,
	DateTime OccurredAt
) : IEvent;