using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.BudgetProgress;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budgets.Queries.GetBudgetProgress;

public sealed class GetBudgetProgressHandler(
	IBudgetProgressReadRepository budgetProgressReadRepository
) : IRequestHandler<GetBudgetProgressQuery, BudgetProgressDto?>
{
	public async Task<BudgetProgressDto?> Handle(
		GetBudgetProgressQuery query,
		CancellationToken ct = default
	) => await budgetProgressReadRepository.GetByBudgetIdAsync(budgetId: query.BudgetId, userId: query.UserId, ct: ct);
}
