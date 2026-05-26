using FinanceTracker.Core.Repositories.Budget;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Queries.GetBudget;

public sealed class GetBudgetHandler(
	IBudgetReadRepository budgetReadRepository
) : IRequestHandler<GetBudgetQuery, Core.Domains.Budget.Budget?>
{
	public async Task<Core.Domains.Budget.Budget?> Handle(
		GetBudgetQuery query,
		CancellationToken ct = default
	) => await budgetReadRepository.GetByIdAsync(budgetId: query.BudgetId, userId: query.UserId, ct: ct);
}
