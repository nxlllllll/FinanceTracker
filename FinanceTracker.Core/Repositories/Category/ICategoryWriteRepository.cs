using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Repositories.Category;

public interface ICategoryWriteRepository
{
	Task CreateAsync(
		Domains.Category.Category category,
		CancellationToken ct = default
	);

	Task RenameAsync(
		Guid categoryId,
		Name newName,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task ChangeParentAsync(
		Guid categoryId,
		Guid? newParentId,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task ArchiveAsync(
		Guid categoryId,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task UnarchiveAsync(
		Guid categoryId,
		int expectedVersion,
		CancellationToken ct = default
	);
}
