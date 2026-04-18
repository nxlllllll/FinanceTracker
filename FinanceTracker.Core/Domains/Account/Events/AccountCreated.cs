using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Account.Events;

public sealed record class AccountCreated(
	Guid Id,
	Guid AccountId,
	Guid UserId,
	string Name,
	string AccountType,
	string Currency,
	decimal Balance,
	DateTime OccurredAt
) : IEvent;