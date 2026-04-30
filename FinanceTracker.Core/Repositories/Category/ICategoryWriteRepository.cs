namespace FinanceTracker.Core.Repositories.Category;

public interface ICategoryWriteRepository
{
	Task CreateAsync(
		Domains.Category.Category category,
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