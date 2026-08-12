namespace FinanceTracker.Core.ReadModels.Pending;

public sealed record PendingRateTransaction(
	Guid TransactionId,
	Guid UserId,
	ValueObjects.Currency TransactionCurrency,
	ValueObjects.Currency BaseCurrency,
	DateTimeOffset OccurredAt,
	DateTimeOffset RateStatusChangedAt
) : IReadModel;
