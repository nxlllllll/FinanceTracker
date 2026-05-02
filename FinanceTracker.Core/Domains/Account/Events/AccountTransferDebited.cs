using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Account.Events;

[EventType(name: "account.transfer_debited")]
public sealed record AccountTransferDebited(
	Guid Id,
	Guid AccountId,
	Guid TransferId,
	Guid ToAccountId,
	decimal Amount,
	decimal ForexRate,
	string? Description,
	DateTime OccurredAt
) : IEvent;