using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.CategoryTotals;
using MediatR;

namespace FinanceTracker.Application.UseCases.Categories.Queries.GetTotal;

public sealed class GetTotalHandler(
	ICategoryTotalReadRepository categoryTotalReadRepository
) : IRequestHandler<GetTotalQuery, CategoryTotalDto?>
{
	public async Task<CategoryTotalDto?> Handle(
		GetTotalQuery query,
		CancellationToken ct = default)
	{
		return await categoryTotalReadRepository.GetByCategoryAsync(
			userId: query.UserId,
			categoryId: query.CategoryId,
			period: query.Period,
			ct: ct
		);
	}
}
