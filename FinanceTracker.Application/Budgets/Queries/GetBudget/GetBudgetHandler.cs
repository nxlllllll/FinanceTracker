using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Budget;
using MediatR;

namespace FinanceTracker.Application.Budgets.Queries.GetBudget;

public sealed class GetBudgetHandler(
	IBudgetReadRepository budgetReadRepository
) : IRequestHandler<GetBudgetQuery, BudgetDto?>
{
	public async Task<BudgetDto?> Handle(
		GetBudgetQuery query,
		CancellationToken ct = default
	) => await budgetReadRepository.GetByIdAsync(budgetId: query.BudgetId, userId: query.UserId, ct: ct);
}