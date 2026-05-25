using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budgets.Queries.GetBudgets;

public sealed class GetBudgetsHandler(
	IBudgetReadRepository budgetReadRepository
) : IRequestHandler<GetBudgetsQuery, PagedResult<Core.Domains.Budget.Budget>>
{
	public async Task<PagedResult<Core.Domains.Budget.Budget>> Handle(
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
