using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels;

public sealed record PendingRateTransfer(
	Guid TransferId,
	Guid FromAccountId,
	Guid ToAccountId,
	decimal AmountFrom,
	Currency CurrencyFrom,
	Currency CurrencyTo,
	decimal CurrentRate,
	int RowVersion,
	DateTimeOffset OccurredAt
) : IReadModel;
