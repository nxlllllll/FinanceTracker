using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetCategory;

public sealed class GetCategoryHandler(
	ICategoryReadRepository categoryReadRepository
) : IRequestHandler<GetCategoryQuery, Result<CategoryReadModel, AppException>>
{
	public async Task<Result<CategoryReadModel, AppException>> Handle(
		GetCategoryQuery query,
		CancellationToken ct = default)
	{
		CategoryReadModel? model = await categoryReadRepository.GetByIdAsync(
			categoryId: query.CategoryId,
			userId: query.UserId,
			ct: ct
		);

		if (model is null)
			return Result<CategoryReadModel, AppException>.Failure(error: new NotFoundException(message: "Category not found.", id: query.CategoryId));

		return Result<CategoryReadModel, AppException>.Success(value: model);
	}
}
