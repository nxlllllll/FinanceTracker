using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Transactions.Events;

public sealed record TransactionIncluded(
	Guid Id,
	Guid TransactionId,
	DateTime OccurredAt
) : IEvent;