using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Queries.GetBudgets;

public sealed class GetBudgetsHandler(
	IBudgetReadRepository budgetReadRepository
) : IRequestHandler<GetBudgetsQuery, PagedResult<BudgetReadModel>>
{
	public async Task<PagedResult<BudgetReadModel>> Handle(
		GetBudgetsQuery query,
		CancellationToken ct = default)
	{
		return await budgetReadRepository.GetAllAsync(
			userId: query.UserId,
			cursorCreatedAt: query.CursorCreatedAt,
			cursorId: query.CursorId,
			pageSize: query.PageSize,
			ct: ct
		);
	}
}
