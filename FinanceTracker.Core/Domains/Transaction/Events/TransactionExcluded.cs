using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Transaction.Events;

public sealed record TransactionExcluded(
	Guid Id,
	Guid TransactionId,
	DateTime OccurredAt
) : IEvent;