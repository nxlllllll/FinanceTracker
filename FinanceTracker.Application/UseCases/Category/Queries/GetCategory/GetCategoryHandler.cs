using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Category;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetCategory;

public sealed class GetCategoryHandler(
	ICategoryReadRepository categoryRepository
) : IRequestHandler<GetCategoryQuery, CategoryReadModel?>
{
	public async Task<CategoryReadModel?> Handle(
		GetCategoryQuery query,
		CancellationToken ct = default
	) => await categoryRepository.GetByIdAsync(categoryId: query.CategoryId, userId: query.UserId, ct: ct);
}