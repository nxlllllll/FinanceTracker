using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Transactions.Events;

public sealed record TransactionCategoryChanged(
	Guid Id,
	Guid TransactionId,
	Guid CategoryId,
	DateTime OccurredAt
) : IEvent;