using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Category.Commands.ArchiveCategory;
using FinanceTracker.Application.UseCases.Category.Commands.RenameCategory;
using FinanceTracker.Application.UseCases.Category.Commands.UnarchiveCategory;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Category.Authorization;

public sealed class CategoryLoader(
	ICategoryRepository categoryRepository
) : IEntityLoader<ArchiveCategoryCommand, Core.Domains.Category.Category, AppException>,
	IEntityLoader<UnarchiveCategoryCommand, Core.Domains.Category.Category, AppException>,
	IEntityLoader<RenameCategoryCommand, Core.Domains.Category.Category, AppException>
{
	public Task<Result<Core.Domains.Category.Category, AppException>> LoadAsync(
		ArchiveCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Category.Category, AppException>> LoadAsync(
		UnarchiveCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Category.Category, AppException>> LoadAsync(
		RenameCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	private async Task<Result<Core.Domains.Category.Category, AppException>> LoadAndAuthorize(Guid categoryId, Guid userId, CancellationToken ct)
	{
		Core.Domains.Category.Category? category = await categoryRepository.GetByIdAsync(categoryId: categoryId, userId: userId, ct: ct);
		if (category is null)
			return Result<Core.Domains.Category.Category, AppException>.Failure(error: new NotFoundException(message: "Category not found.", id: categoryId));

		return Result<Core.Domains.Category.Category, AppException>.Success(value: category);
	}
}
