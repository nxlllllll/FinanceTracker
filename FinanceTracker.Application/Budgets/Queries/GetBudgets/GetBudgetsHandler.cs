using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Budget;
using MediatR;

namespace FinanceTracker.Application.Budgets.Queries.GetBudgets;

public sealed class GetBudgetsHandler(
	IBudgetReadRepository budgetReadRepository
) : IRequestHandler<GetBudgetsQuery, IReadOnlyList<BudgetDto>>
{
	public async Task<IReadOnlyList<BudgetDto>> Handle(
		GetBudgetsQuery query,
		CancellationToken ct = default
	) => await budgetReadRepository.GetAllAsync(userId: query.UserId, ct: ct);
}