using FinanceTracker.Core.Repositories.Category;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetTotal;

public sealed class GetTotalHandler(
	ICategoryTotalReadRepository categoryTotalReadRepository
) : IRequestHandler<GetTotalQuery, CategoryTotal?>
{
	public async Task<CategoryTotal?> Handle(
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
