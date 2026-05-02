using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Account.Events;

[EventType(name: "account.transfer_credited")]
public sealed record AccountTransferCredited(
	Guid Id,
	Guid AccountId,
	Guid TransferId,
	Guid FromAccountId,
	decimal Amount,
	decimal ExchangeRate,
	string? Description,
	DateTime OccurredAt
) : IEvent;