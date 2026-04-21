using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Transactions.Events;

public sealed record TransactionDescriptionChanged(
	Guid Id,
	Guid TransactionId,
	string? Description,
	DateTime OccurredAt
) : IEvent;