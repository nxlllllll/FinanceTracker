using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Budget;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Queries.GetBudget;

public sealed class GetBudgetHandler(
	IBudgetReadRepository budgetReadRepository
) : IRequestHandler<GetBudgetQuery, BudgetReadModel?>
{
	public async Task<BudgetReadModel?> Handle(
		GetBudgetQuery query,
		CancellationToken ct = default
	) => await budgetReadRepository.GetByIdAsync(budgetId: query.BudgetId, userId: query.UserId, ct: ct);
}