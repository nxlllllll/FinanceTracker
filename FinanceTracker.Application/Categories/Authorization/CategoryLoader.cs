using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Categories.Commands.ArchiveCategory;
using FinanceTracker.Application.Categories.Commands.RenameCategory;
using FinanceTracker.Application.Categories.Commands.UnarchiveCategory;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;

namespace FinanceTracker.Application.Categories.Authorization;

public sealed class CategoryLoader(
	ICategoryReadRepository categoryReadRepository
) : IEntityLoader<ArchiveCategoryCommand, Category>,
	IEntityLoader<UnarchiveCategoryCommand, Category>,
	IEntityLoader<RenameCategoryCommand, Category>
{
	public Task<Category> LoadAsync(
		ArchiveCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	public Task<Category> LoadAsync(
		UnarchiveCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	public Task<Category> LoadAsync(
		RenameCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	private async Task<Category> LoadAndAuthorize(Guid categoryId, Guid userId, CancellationToken ct)
	{
		Category category = await categoryReadRepository.GetByIdAsync(categoryId: categoryId, ct: ct)
			?? throw new NotFoundException(message: "Category not found.", id: categoryId);

		if (category.UserId != userId)
			throw new NotFoundException(message: "Category not found.", id: categoryId);

		return category;
	}
}