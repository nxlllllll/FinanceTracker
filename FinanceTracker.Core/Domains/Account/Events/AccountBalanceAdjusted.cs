using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Account.Events;

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