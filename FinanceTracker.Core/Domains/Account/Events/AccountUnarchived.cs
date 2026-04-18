using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Account.Events;

public record AccountUnarchived(
	Guid Id,
	Guid AccountId,
	DateTime OccurredAt
) : IEvent;