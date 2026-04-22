using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Account.Events;

public sealed record AccountDebited(
	Guid Id,
	Guid AccountId,
	Guid TransactionId,
	Guid CategoryId,
	decimal Amount,
	decimal ExchangeRate,
	string? Description,
	DateTime OccurredAt
) : IEvent;