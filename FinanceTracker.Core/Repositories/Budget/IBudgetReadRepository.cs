namespace FinanceTracker.Core.Repositories.Budget;

public interface IBudgetReadRepository
{
	Task<Domains.Budget.Budget?> GetByIdAsync(
		Guid budgetId,
		Guid userId,
		CancellationToken ct = default
	);

	Task<Domains.Budget.Budget?> GetActiveByCategoryAsync(
		Guid userId,
		Guid categoryId,
		DateOnly date,
		CancellationToken ct = default
	);

	Task<bool> HasOverlappingAsync(
		Guid userId,
		Guid categoryId,
		DateOnly from,
		DateOnly to,
		Guid? excludeBudgetId = null,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<Domains.Budget.Budget>> GetAllAsync(
		Guid userId,
		DateTime? cursorCreatedAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default
	);
}