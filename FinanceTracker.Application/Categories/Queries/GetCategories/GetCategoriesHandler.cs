using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories.Category;
using MediatR;

namespace FinanceTracker.Application.Categories.Queries.GetCategories;

public sealed class GetCategoriesHandler(
	ICategoryReadRepository categoryRepository
) : IRequestHandler<GetCategoriesQuery, IReadOnlyList<Category>>
{
	public async Task<IReadOnlyList<Category>> Handle(
		GetCategoriesQuery query,
		CancellationToken ct)
	{
		return await categoryRepository.GetAllAsync(
			userId: query.UserId,
			type: query.Type,
			isArchived: query.IsArchived,
			parentId: query.ParentId,
			ct: ct
		);
	}
}