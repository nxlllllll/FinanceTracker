namespace FinanceTracker.Core.Repositories.User;

public sealed record TransferDetails(
	Guid FromAccountId,
	Guid ToAccountId,
	decimal AmountFrom,
	ValueObjects.Currency? CurrencyFrom,
	decimal AmountTo,
	ValueObjects.Currency? CurrencyTo
);
