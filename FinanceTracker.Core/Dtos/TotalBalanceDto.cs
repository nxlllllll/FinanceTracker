using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Dtos;

public sealed record TotalBalanceDto(
	decimal Balance,
	Currency Currency
);