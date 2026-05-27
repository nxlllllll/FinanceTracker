namespace FinanceTracker.Core.Repositories.Category;

public interface ICategoryTotalReadRepository
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
