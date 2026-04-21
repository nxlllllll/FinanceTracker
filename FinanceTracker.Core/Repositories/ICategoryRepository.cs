using FinanceTracker.Core.Domains.Category;

namespace FinanceTracker.Core.Repositories;

public interface ICategoryRepository
{
	Task<Category?> GetByIdAsync(
		Guid categoryId,
		CancellationToken ct = default
	);

	Task CreateAsync(
		Category category,
		CancellationToken ct = default
	);

	Task RenameAsync(
		Guid categoryId,
		string newName,
		CancellationToken ct = default
	);

	Task ArchiveAsync(
		Guid categoryId,
		CancellationToken ct = default
	);

	Task UnarchiveAsync(
		Guid categoryId,
		CancellationToken ct = default
	);
}