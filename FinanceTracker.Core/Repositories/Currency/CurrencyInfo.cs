namespace FinanceTracker.Core.Repositories.Currency;

public sealed record CurrencyInfo(
	string Code,
	string Name,
	string Symbol,
	bool IsActive
);
