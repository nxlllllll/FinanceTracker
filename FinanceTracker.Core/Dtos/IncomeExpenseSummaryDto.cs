using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Dtos;

public sealed record IncomeExpenseSummaryDto(
	decimal Income,
	decimal Expense,
	Currency Currency,
	DateOnly Period
);
