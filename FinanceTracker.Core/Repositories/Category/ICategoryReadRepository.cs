using FinanceTracker.Core.Domains.Category;

namespace FinanceTracker.Core.Repositories.Category;

public interface ICategoryReadRepository
{
	Task<Domains.Category.Category?> GetByIdAsync(
		Guid categoryId,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<Domains.Category.Category>> GetAllAsync(
		Guid userId,
		CategoryType? type = null,
		bool? isArchived = null,
		Guid? parentId = null,
		DateTime? cursorCreatedAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default
	);
}