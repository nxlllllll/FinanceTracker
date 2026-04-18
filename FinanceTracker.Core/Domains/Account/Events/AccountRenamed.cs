using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Account.Events;

public record AccountRenamed(
	Guid Id,
	Guid AccountId,
	string NewName,
	DateTime OccurredAt
) : IEvent;