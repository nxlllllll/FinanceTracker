using FinanceTracker.Core.Domains.Abstractions.ES.Event;

namespace FinanceTracker.Core.Domains.Account.Events;

[EventType(name: "account.debited")]
public sealed record AccountDebited(
	Guid Id,
	Guid AccountId,
	Guid TransactionId,
	Guid CategoryId,
	decimal Amount,
	decimal ExchangeRate,
	string? Description,
	DateTimeOffset OccurredAt
) : IEvent;
