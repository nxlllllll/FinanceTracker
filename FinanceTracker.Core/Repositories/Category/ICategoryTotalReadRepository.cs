using FinanceTracker.Core.ReadModels;

namespace FinanceTracker.Core.Repositories.Category;

public interface ICategoryTotalReadRepository : IReadRepository<CategoryTotal>
{
	Task<CategoryTotal?> GetByCategoryAsync(
		Guid userId,
		Guid categoryId,
		DateOnly period,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<CategoryTotal>> GetAllByPeriodAsync(
		Guid userId,
		DateOnly period,
		CancellationToken ct = default
	);
}
