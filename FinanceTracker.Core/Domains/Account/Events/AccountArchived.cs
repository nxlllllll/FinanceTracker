using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Account.Events;

public sealed record AccountArchived(
	Guid Id,
	Guid AccountId,
	DateTime OccurredAt
) : IEvent;