using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Category;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetTotalsByPeriod;

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
