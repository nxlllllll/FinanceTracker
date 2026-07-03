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
) : IEntityLoader<ArchiveCategoryCommand, Core.Domains.Category.Category, DomainException>,
	IEntityLoader<UnarchiveCategoryCommand, Core.Domains.Category.Category, DomainException>,
	IEntityLoader<RenameCategoryCommand, Core.Domains.Category.Category, DomainException>
{
	public Task<Result<Core.Domains.Category.Category, DomainException>> LoadAsync(
		ArchiveCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Category.Category, DomainException>> LoadAsync(
		UnarchiveCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Category.Category, DomainException>> LoadAsync(
		RenameCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(categoryId: request.CategoryId, userId: request.UserId, ct: ct);

	private async Task<Result<Core.Domains.Category.Category, DomainException>> LoadAndAuthorize(Guid categoryId, Guid userId, CancellationToken ct)
	{
		Core.Domains.Category.Category? category = await categoryRepository.GetByIdAsync(categoryId: categoryId, userId: userId, ct: ct);
		if (category is null)
			return Result<Core.Domains.Category.Category, DomainException>.Failure(error: new NotFoundException(message: "Category not found.", id: categoryId));

		return Result<Core.Domains.Category.Category, DomainException>.Success(value: category);
	}
}
