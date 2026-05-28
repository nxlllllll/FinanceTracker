namespace FinanceTracker.Core.ReadModels;

public sealed record CurrencyInfo(
	string Code,
	string Name,
	string Symbol,
	bool IsActive
) : IReadModel;