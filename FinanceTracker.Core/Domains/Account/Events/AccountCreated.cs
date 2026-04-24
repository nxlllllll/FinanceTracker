using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Account.Events;

public sealed record AccountCreated(
	Guid Id,
	Guid AccountId,
	Guid UserId,
	string Name,
	AccountType Type,
	string Currency,
	decimal Balance,
	DateTime OccurredAt
) : IEvent;