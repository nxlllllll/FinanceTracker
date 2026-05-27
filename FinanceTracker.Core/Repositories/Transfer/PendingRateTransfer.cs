namespace FinanceTracker.Core.Repositories.Transfer;

public sealed record PendingRateTransfer(
	Guid TransferId,
	Guid FromAccountId,
	Guid ToAccountId,
	decimal AmountFrom,
	ValueObjects.Currency CurrencyFrom,
	ValueObjects.Currency CurrencyTo,
	decimal CurrentRate,
	DateTimeOffset OccurredAt
);
