using FinanceTracker.Core.ReadModels.Budget;

namespace FinanceTracker.Core.Repositories.Budget;

public interface IBudgetProgressReadRepository : IReadRepository<BudgetProgress>
{
	Task<BudgetProgress?> GetByBudgetIdAsync(
		Guid budgetId,
		Guid userId,
		CancellationToken ct = default
	);
}
