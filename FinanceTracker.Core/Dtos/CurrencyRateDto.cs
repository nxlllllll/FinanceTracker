using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Dtos;

public sealed record CurrencyRateDto(
	Currency Base,
	Currency Target,
	decimal Rate,
	DateOnly Date
);
