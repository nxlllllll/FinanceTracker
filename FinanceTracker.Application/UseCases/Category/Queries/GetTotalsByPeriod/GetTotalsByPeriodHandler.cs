using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetTotalsByPeriod;

public sealed class GetTotalsByPeriodHandler(
	ICategoryTotalReadRepository categoryTotalReadRepository,
	IBaseCurrencyRecalculationReadRepository recalculationReadRepository
) : IRequestHandler<GetTotalsByPeriodQuery, Result<CategoryTotalsView, AppException>>
{
	public async Task<Result<CategoryTotalsView, AppException>> Handle(
		GetTotalsByPeriodQuery query,
		CancellationToken ct = default)
	{
		bool isUnavailable = await recalculationReadRepository.TotalsAreUnavailableAsync(userId: query.UserId, ct: ct);
		if (isUnavailable)
			return Result<CategoryTotalsView, AppException>.Success(value: CategoryTotalsView.Pending());

		return Result<CategoryTotalsView, AppException>.Success(value: CategoryTotalsView.Ready(totals: await categoryTotalReadRepository.GetAllByPeriodAsync(
			userId: query.UserId,
			period: query.Period,
			ct: ct
		)));
	}
}
