namespace FinanceTracker.Core.Repositories.Category;

public interface ICategoryRepository
{
	Task<Domains.Category.Category?> GetByIdAsync(
		Guid categoryId,
		Guid userId,
		CancellationToken ct = default
	);
}
