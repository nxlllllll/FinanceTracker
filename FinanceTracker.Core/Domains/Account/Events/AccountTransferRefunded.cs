using FinanceTracker.Core.Domains.Abstractions.ES.Event;

namespace FinanceTracker.Core.Domains.Account.Events;

[EventType(name: "account.transfer_refunded")]
public sealed record AccountTransferRefunded(
	Guid Id,
	Guid AccountId,
	Guid TransferId,
	decimal Amount,
	string? Description,
	DateTimeOffset OccurredAt
) : IEvent;
