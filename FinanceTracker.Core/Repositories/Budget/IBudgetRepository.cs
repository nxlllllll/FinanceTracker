namespace FinanceTracker.Core.Repositories.Budget;

public interface IBudgetRepository
{
	Task<Domains.Budget.Budget?> GetByIdAsync(
		Guid budgetId,
		Guid userId,
		CancellationToken ct = default
	);
}
