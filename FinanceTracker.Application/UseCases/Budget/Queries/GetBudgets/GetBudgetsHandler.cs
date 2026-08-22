using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Budget;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Queries.GetBudgets;

public sealed class GetBudgetsHandler(
	IBudgetReadRepository budgetReadRepository
) : IRequestHandler<GetBudgetsQuery, Result<PagedResult<BudgetReadModel>, AppException>>
{
	public async Task<Result<PagedResult<BudgetReadModel>, AppException>> Handle(
		GetBudgetsQuery query,
		CancellationToken ct = default)
	{
		return Result<PagedResult<BudgetReadModel>, AppException>.Success(value: await budgetReadRepository.GetAllAsync(
			userId: query.UserId,
			cursorCreatedAt: query.CursorCreatedAt,
			cursorId: query.CursorId,
			pageSize: query.PageSize,
			ct: ct
		));
	}
}
