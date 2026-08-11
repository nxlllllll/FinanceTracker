namespace FinanceTracker.Core.ReadModels.Budget;

public sealed record BudgetProgress(
	Guid BudgetId,
	decimal Spent,
	decimal Remaining,
	decimal Percentage,
	DateTimeOffset UpdatedAt
) : IReadModel;
