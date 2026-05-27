namespace FinanceTracker.Core.Repositories.Budget;

public interface IBudgetProgressReadRepository
{
	Task<BudgetProgress?> GetByBudgetIdAsync(
		Guid budgetId,
		Guid userId,
		CancellationToken ct = default
	);
}
