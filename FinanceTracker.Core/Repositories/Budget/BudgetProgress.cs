namespace FinanceTracker.Core.Repositories.Budget;

public sealed record BudgetProgress(
	Guid BudgetId,
	decimal Spent,
	decimal Remaining,
	decimal Percentage,
	DateTimeOffset UpdatedAt
);
