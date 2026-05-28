namespace FinanceTracker.Core.ReadModels;

public sealed record BudgetProgress(
	Guid BudgetId,
	decimal Spent,
	decimal Remaining,
	decimal Percentage,
	DateTimeOffset UpdatedAt
) : IReadModel;