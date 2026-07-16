using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels;

public sealed record PendingRateTransfer(
	Guid TransferId,
	Currency CurrencyFrom,
	Currency CurrencyTo,
	DateTimeOffset OccurredAt,
	DateTimeOffset RateStatusChangedAt
) : IReadModel;
