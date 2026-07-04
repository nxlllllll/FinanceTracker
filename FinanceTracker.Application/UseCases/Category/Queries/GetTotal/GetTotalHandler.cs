using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetTotal;

public sealed class GetTotalHandler(
	ICategoryTotalReadRepository categoryTotalReadRepository
) : IRequestHandler<GetTotalQuery, Result<CategoryTotal, AppException>>
{
	public async Task<Result<CategoryTotal, AppException>> Handle(
		GetTotalQuery query,
		CancellationToken ct = default)
	{

		CategoryTotal? total = await categoryTotalReadRepository.GetByCategoryAsync(
			userId: query.UserId,
			categoryId: query.CategoryId,
			period: query.Period,
			ct: ct
		);

		return Result<CategoryTotal, AppException>.Success(value: total ?? new CategoryTotal(
			CategoryId: query.CategoryId,
			Period: query.Period,
			Total: 0m,
			Count: 0,
			UpdatedAt: null
		));
	}
}
