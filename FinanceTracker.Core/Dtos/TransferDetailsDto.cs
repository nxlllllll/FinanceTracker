using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Dtos;

public sealed record TransferDetailsDto(
	Guid FromAccountId,
	Guid ToAccountId,
	decimal AmountFrom,
	Currency? CurrencyFrom,
	decimal AmountTo,
	Currency? CurrencyTo
);