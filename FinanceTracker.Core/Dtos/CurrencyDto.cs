namespace FinanceTracker.Core.Dtos;

public sealed record CurrencyDto(
	string Code,
	string Name,
	string Symbol,
	bool IsActive
);