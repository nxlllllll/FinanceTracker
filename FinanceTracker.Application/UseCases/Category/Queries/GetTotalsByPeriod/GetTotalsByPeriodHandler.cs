using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetTotalsByPeriod;

public sealed class GetTotalsByPeriodHandler(
	ICategoryTotalReadRepository categoryTotalReadRepository
) : IRequestHandler<GetTotalsByPeriodQuery, Result<IReadOnlyList<CategoryTotal>, AppException>>
{
	public async Task<Result<IReadOnlyList<CategoryTotal>, AppException>> Handle(
		GetTotalsByPeriodQuery query,
		CancellationToken ct = default)
	{
		return Result<IReadOnlyList<CategoryTotal>, AppException>.Success(value: await categoryTotalReadRepository.GetAllByPeriodAsync(
			userId: query.UserId,
			period: query.Period,
			ct: ct
		));
	}
}
