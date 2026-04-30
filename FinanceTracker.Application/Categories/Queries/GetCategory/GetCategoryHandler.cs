using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories.Category;
using MediatR;

namespace FinanceTracker.Application.Categories.Queries.GetCategory;

public sealed class GetCategoryHandler(
	ICategoryReadRepository categoryRepository
) : IRequestHandler<GetCategoryQuery, Category?>
{
	public async Task<Category?> Handle(
		GetCategoryQuery query,
		CancellationToken ct = default
	) => await categoryRepository.GetByIdAsync(categoryId: query.CategoryId, ct: ct);
}