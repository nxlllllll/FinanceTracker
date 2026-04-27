using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.BudgetProgress;
using MediatR;

namespace FinanceTracker.Application.Budgets.Queries.GetBudgetProgress;

public sealed class GetBudgetProgressHandler(
	IBudgetProgressReadRepository budgetProgressReadRepository
) : IRequestHandler<GetBudgetProgressQuery, BudgetProgressDto?>
{
	public async Task<BudgetProgressDto?> Handle(
		GetBudgetProgressQuery query,
		CancellationToken ct = default
	) => await budgetProgressReadRepository.GetByBudgetIdAsync(budgetId: query.BudgetId, ct: ct);
}