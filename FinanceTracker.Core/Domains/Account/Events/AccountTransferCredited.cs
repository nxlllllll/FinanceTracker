using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

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
	int Version,
	DateTimeOffset OccurredAt
) : IEvent
{
	public IEvent WithVersion(int version) => this with { Version = version };
}
