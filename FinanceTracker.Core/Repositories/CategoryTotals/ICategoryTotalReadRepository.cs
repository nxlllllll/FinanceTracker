using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories.CategoryTotals;

public interface ICategoryTotalReadRepository
{
	Task<CategoryTotalDto?> GetByCategoryAsync(
		Guid userId,
		Guid categoryId,
		DateOnly period,
		CancellationToken ct = default
	);
	
	Task<IReadOnlyList<CategoryTotalDto>> GetAllByPeriodAsync(
		Guid userId,
		DateOnly period,
		CancellationToken ct = default
	);
}
