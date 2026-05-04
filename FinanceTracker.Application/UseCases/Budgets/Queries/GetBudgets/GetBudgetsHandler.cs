using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Repositories.Budget;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budgets.Queries.GetBudgets;

public sealed class GetBudgetsHandler(
	IBudgetReadRepository budgetReadRepository
) : IRequestHandler<GetBudgetsQuery, IReadOnlyList<Budget>>
{
	public async Task<IReadOnlyList<Budget>> Handle(
		GetBudgetsQuery query,
		CancellationToken ct = default
	) => await budgetReadRepository.GetAllAsync(userId: query.UserId, ct: ct);
}