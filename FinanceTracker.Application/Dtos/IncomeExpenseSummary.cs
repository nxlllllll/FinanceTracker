using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Application.Dtos;

public sealed record IncomeExpenseSummary(
	decimal Income,
	decimal Expense,
	Currency Currency,
	DateOnly Period
);
