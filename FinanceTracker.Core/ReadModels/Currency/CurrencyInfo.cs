namespace FinanceTracker.Core.ReadModels.Currency;

public sealed record CurrencyInfo(
	string Code,
	string Name,
	string Symbol,
	bool IsActive
) : IReadModel;
