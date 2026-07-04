using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetCategories;

public sealed class GetCategoriesHandler(
	ICategoryReadRepository categoryReadRepository
) : IRequestHandler<GetCategoriesQuery, Result<PagedResult<CategoryReadModel>, AppException>>
{
	public async Task<Result<PagedResult<CategoryReadModel>, AppException>> Handle(
		GetCategoriesQuery query,
		CancellationToken ct = default)
	{
		return Result<PagedResult<CategoryReadModel>, AppException>.Success(value: await categoryReadRepository.GetAllAsync(
			userId: query.UserId,
			type: query.Type,
			isArchived: query.IsArchived,
			parentId: query.ParentId,
			cursorCreatedAt: query.CursorCreatedAt,
			cursorId: query.CursorId,
			pageSize: query.PageSize,
			ct: ct
		));
	}
}
