using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels;

public sealed record TransferDetails(
	Guid FromAccountId,
	Guid ToAccountId,
	decimal AmountFrom,
	Currency? CurrencyFrom,
	decimal AmountTo,
	Currency? CurrencyTo
) : IReadModel;