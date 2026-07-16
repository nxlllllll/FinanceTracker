using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels;

public sealed record PendingRateTransaction(
	Guid TransactionId,
	Guid UserId,
	Currency TransactionCurrency,
	Currency BaseCurrency,
	DateTimeOffset OccurredAt,
	DateTimeOffset RateStatusChangedAt
) : IReadModel;
