namespace FinanceTracker.Core.Dtos;

public sealed record BudgetProgressDto(
	Guid BudgetId,
	decimal Spent,
	decimal Remaining,
	decimal Percentage,
	DateTimeOffset UpdatedAt
);
