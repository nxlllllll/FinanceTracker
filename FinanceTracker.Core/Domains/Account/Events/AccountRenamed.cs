using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Account.Events;

[EventType(name: "account.renamed")]
public sealed record AccountRenamed(
	Guid Id,
	Guid AccountId,
	Name NewName,
	DateTime OccurredAt
) : IEvent;