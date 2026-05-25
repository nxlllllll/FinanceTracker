using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Repositories.Category;

public interface ICategoryReadRepository
{
	Task<Domains.Category.Category?> GetByIdAsync(
		Guid categoryId,
		Guid userId,
		CancellationToken ct = default
	);

	Task<PagedResult<Domains.Category.Category>> GetAllAsync(
		Guid userId,
		CategoryType? type = null,
		bool? isArchived = null,
		Guid? parentId = null,
		DateTimeOffset? cursorCreatedAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default
	);
}
