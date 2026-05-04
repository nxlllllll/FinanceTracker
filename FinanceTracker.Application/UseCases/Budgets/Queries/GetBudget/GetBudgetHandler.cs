using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Repositories.Budget;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budgets.Queries.GetBudget;

public sealed class GetBudgetHandler(
	IBudgetReadRepository budgetReadRepository
) : IRequestHandler<GetBudgetQuery, Budget?>
{
	public async Task<Budget?> Handle(
		GetBudgetQuery query,
		CancellationToken ct = default
	) => await budgetReadRepository.GetByIdAsync(budgetId: query.BudgetId, userId: query.UserId, ct: ct);
}