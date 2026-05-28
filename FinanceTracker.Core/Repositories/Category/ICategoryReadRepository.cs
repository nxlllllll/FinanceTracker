using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Repositories.Category;

public interface ICategoryReadRepository : IReadRepository<CategoryReadModel>
{
	Task<CategoryReadModel?> GetByIdAsync(
		Guid categoryId,
		Guid userId,
		CancellationToken ct = default
	);

	Task<PagedResult<CategoryReadModel>> GetAllAsync(
		Guid userId,
		Domains.Category.CategoryType? type = null,
		bool? isArchived = null,
		Guid? parentId = null,
		DateTimeOffset? cursorCreatedAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default
	);
}