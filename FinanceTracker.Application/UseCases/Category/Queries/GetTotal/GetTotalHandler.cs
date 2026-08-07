using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetTotal;

public sealed class GetTotalHandler(
	ICategoryTotalReadRepository categoryTotalReadRepository,
	IBaseCurrencyRecalculationReadRepository recalculationReadRepository
) : IRequestHandler<GetTotalQuery, Result<CategoryTotalView, AppException>>
{
	public async Task<Result<CategoryTotalView, AppException>> Handle(
		GetTotalQuery query,
		CancellationToken ct = default)
	{
		bool isUnavailable = await recalculationReadRepository.TotalsAreUnavailableAsync(userId: query.UserId, ct: ct);
		if (isUnavailable)
			return Result<CategoryTotalView, AppException>.Success(value: CategoryTotalView.Pending());

		CategoryTotal? total = await categoryTotalReadRepository.GetByCategoryAsync(
			userId: query.UserId,
			categoryId: query.CategoryId,
			period: query.Period,
			ct: ct
		);

		return Result<CategoryTotalView, AppException>.Success(value: CategoryTotalView.Ready(total: total ?? new CategoryTotal(
			CategoryId: query.CategoryId,
			Period: query.Period,
			Total: 0m,
			Count: 0,
			UpdatedAt: null
		)));
	}
}
