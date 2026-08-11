namespace FinanceTracker.Core.ReadModels.Pending;

public sealed record PendingRateTransfer(
	Guid TransferId,
	ValueObjects.Currency CurrencyFrom,
	ValueObjects.Currency CurrencyTo,
	DateTimeOffset OccurredAt,
	DateTimeOffset RateStatusChangedAt
) : IReadModel;
