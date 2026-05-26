using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories.Budget;

public interface IBudgetProgressReadRepository
{
	Task<BudgetProgressDto?> GetByBudgetIdAsync(
		Guid budgetId,
		Guid userId,
		CancellationToken ct = default
	);
}
