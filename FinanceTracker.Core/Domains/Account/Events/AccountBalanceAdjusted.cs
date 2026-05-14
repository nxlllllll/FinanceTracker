using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Abstractions.ES;
using FinanceTracker.Core.Domains.Abstractions.ES.Event;

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
	DateTime OccurredAt
) : IEvent;