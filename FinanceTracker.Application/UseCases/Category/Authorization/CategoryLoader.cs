using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Category.Commands.ArchiveCategory;
using FinanceTracker.Application.UseCases.Category.Commands.RenameCategory;
using FinanceTracker.Application.UseCases.Category.Commands.UnarchiveCategory;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Category.Authorization;

public sealed class CategoryLoader(
	ICategoryRepository categoryRepository
) : IEntityLoader<ArchiveCategoryCommand, Core.Domains.Category.Category, NotFoundException>,
	IEntityLoader<UnarchiveCategoryCommand, Core.Domains.Category.Category, NotFoundException>,
	IEntityLoader<RenameCategoryCommand, Core.Domains.Category.Category, NotFoundException>
{
	public Task<Result<Core.Domains.Category.Category, NotFoundException>> LoadAsync(
		ArchiveCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Category.Category, NotFoundException>> LoadAsync(
		UnarchiveCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Category.Category, NotFoundException>> LoadAsync(
		RenameCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	private async Task<Result<Core.Domains.Category.Category, NotFoundException>> LoadAndAuthorize(Guid categoryId, Guid userId, CancellationToken ct)
	{
		Core.Domains.Category.Category? category = await categoryRepository.GetByIdAsync(categoryId: categoryId, userId: userId, ct: ct);
		if (category is null)
			return Result<Core.Domains.Category.Category, NotFoundException>.Failure(error: new NotFoundException(message: "Category not found.", id: categoryId));

		return Result<Core.Domains.Category.Category, NotFoundException>.Success(value: category);
	}
}
