using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Budget;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Repositories.Budget;

public interface IBudgetReadRepository : IReadRepository<BudgetReadModel>
{
	Task<BudgetReadModel?> GetByIdAsync(
		Guid budgetId,
		Guid userId,
		CancellationToken ct = default
	);

	Task<BudgetReadModel?> GetActiveByCategoryAsync(
		Guid userId,
		Guid categoryId,
		DateOnly date,
		CancellationToken ct = default
	);

	Task<Guid?> FindOverlappingAsync(
		Guid userId,
		Guid categoryId,
		DateOnly from,
		DateOnly to,
		Guid? excludeBudgetId = null,
		CancellationToken ct = default
	);

	Task<PagedResult<BudgetReadModel>> GetAllAsync(
		Guid userId,
		DateTimeOffset? cursorCreatedAt = null,
		bool? isActive = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default
	);
}
