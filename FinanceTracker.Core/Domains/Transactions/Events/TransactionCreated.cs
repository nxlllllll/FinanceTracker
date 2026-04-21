using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Transactions.Events;

public sealed record TransactionCreated(
	Guid Id,
	Guid TransactionId,
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	decimal Amount,
	DirectionType Direction,
	decimal ExchangeRate,
	string? Description,
	DateTime OccurredAt
) : IEvent;