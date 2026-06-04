using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.Account.Events;

[EventType(name: "account.balance_adjusted")]
public sealed record AccountBalanceAdjusted(
	Guid Id,
	Guid AccountId,
	Guid SourceId,
	string SourceType,
	decimal OldRate,
	decimal NewRate,
	decimal Amount,
	decimal Delta,
	int Version,
	DateTimeOffset OccurredAt
) : IEvent
{
	public IEvent WithVersion(int version) => this with { Version = version };
}