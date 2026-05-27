using FinanceTracker.Core.Repositories.Budget;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Queries.GetBudgetProgress;

public sealed class GetBudgetProgressHandler(
	IBudgetProgressReadRepository budgetProgressReadRepository
) : IRequestHandler<GetBudgetProgressQuery, BudgetProgress?>
{
	public async Task<BudgetProgress?> Handle(
		GetBudgetProgressQuery query,
		CancellationToken ct = default
	) => await budgetProgressReadRepository.GetByBudgetIdAsync(budgetId: query.BudgetId, userId: query.UserId, ct: ct);
}
