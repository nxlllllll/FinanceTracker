using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Account.Events;

public sealed record AccountTransferDebited(
	Guid Id,
	Guid AccountId,
	Guid TransferId,
	Guid ToAccountId,
	decimal Amount,
	decimal ExchangeRate,
	string? Description,
	DateTime OccurredAt
) : IEvent;