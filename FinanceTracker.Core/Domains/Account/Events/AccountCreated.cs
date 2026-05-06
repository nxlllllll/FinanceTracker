using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Account.Events;

[EventType(name: "account.created")]
public sealed record AccountCreated(
	Guid Id,
	Guid AccountId,
	Guid UserId,
	string Name,
	AccountType Type,
	Currency Currency,
	decimal Balance,
	DateTime OccurredAt
) : IEvent;