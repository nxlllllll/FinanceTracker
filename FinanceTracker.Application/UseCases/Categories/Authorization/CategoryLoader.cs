using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Categories.Commands.ArchiveCategory;
using FinanceTracker.Application.UseCases.Categories.Commands.RenameCategory;
using FinanceTracker.Application.UseCases.Categories.Commands.UnarchiveCategory;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Categories.Authorization;

public sealed class CategoryLoader(
	ICategoryReadRepository categoryReadRepository
) : IEntityLoader<ArchiveCategoryCommand, Category, NotFoundException>,
	IEntityLoader<UnarchiveCategoryCommand, Category, NotFoundException>,
	IEntityLoader<RenameCategoryCommand, Category, NotFoundException>
{
	public Task<Result<Category, NotFoundException>> LoadAsync(
		ArchiveCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	public Task<Result<Category, NotFoundException>> LoadAsync(
		UnarchiveCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	public Task<Result<Category, NotFoundException>> LoadAsync(
		RenameCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	private async Task<Result<Category, NotFoundException>> LoadAndAuthorize(Guid categoryId, Guid userId, CancellationToken ct)
	{
		Category? category = await categoryReadRepository.GetByIdAsync(categoryId: categoryId, ct: ct);
		if (category is null || category.UserId != userId)
			return Result<Category, NotFoundException>.Failure(error: new NotFoundException(message: "Category not found.", id: categoryId));

		return Result<Category, NotFoundException>.Success(value: category);
	}
}