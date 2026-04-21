using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Transactions.Events;

public sealed record TransactionExcluded(
	Guid Id,
	Guid TransactionId,
	DateTime OccurredAt
) : IEvent;