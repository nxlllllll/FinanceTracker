using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Account.Events;

public sealed record AccountRenamed(
	Guid Id,
	Guid AccountId,
	string NewName,
	DateTime OccurredAt
) : IEvent;