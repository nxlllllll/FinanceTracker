using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Categories.Queries.GetCategories;

public sealed class GetCategoriesHandler(
	ICategoryReadRepository categoryReadRepository
) : IRequestHandler<GetCategoriesQuery, PagedResult<Core.Domains.Category.Category>>
{
	public async Task<PagedResult<Core.Domains.Category.Category>> Handle(
		GetCategoriesQuery query,
		CancellationToken ct = default)
	{
		return await categoryReadRepository.GetAllAsync(
			userId: query.UserId,
			type: query.Type,
			isArchived: query.IsArchived,
			parentId: query.ParentId,
			cursorCreatedAt: query.CursorCreatedAt,
			cursorId: query.CursorId,
			pageSize: query.PageSize,
			ct: ct
		);
	}
}