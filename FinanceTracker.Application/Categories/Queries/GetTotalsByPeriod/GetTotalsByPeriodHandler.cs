using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.CategoryTotals;
using MediatR;

namespace FinanceTracker.Application.Categories.Queries.GetTotalsByPeriod;

public sealed class GetTotalsByPeriodHandler(
	ICategoryTotalReadRepository categoryTotalReadRepository
) : IRequestHandler<GetTotalsByPeriodQuery, IReadOnlyList<CategoryTotalDto>>
{
	public async Task<IReadOnlyList<CategoryTotalDto>> Handle(
		GetTotalsByPeriodQuery query,
		CancellationToken ct = default)
	{
		return await categoryTotalReadRepository.GetAllByPeriodAsync(
			userId: query.UserId,
			period: query.Period,
			ct: ct
		);
	}
}