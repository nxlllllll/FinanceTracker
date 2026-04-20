using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.Categories.Queries.GetCategory;

public sealed class GetCategoryHandler(
	ICategoryRepository categoryRepository
) : IRequestHandler<GetCategoryQuery, Category?>
{
	public async Task<Category?> Handle(
		GetCategoryQuery query,
		CancellationToken ct = default
	) => await categoryRepository.GetByIdAsync(categoryId: query.CategoryId, ct: ct);
}