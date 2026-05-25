using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Dtos;

public sealed record PendingRateTransfer(
	Guid TransferId,
	Guid FromAccountId,
	Guid ToAccountId,
	decimal AmountFrom,
	Currency CurrencyFrom,
	Currency CurrencyTo,
	decimal CurrentRate,
	DateTimeOffset OccurredAt
);
