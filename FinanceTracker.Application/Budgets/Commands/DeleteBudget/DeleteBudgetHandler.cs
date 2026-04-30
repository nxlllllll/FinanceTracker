using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Budget;

namespace FinanceTracker.Application.Budgets.Commands.DeleteBudget;

public sealed class DeleteBudgetHandler(
	IBudgetWriteRepository budgetWriteRepository
) : IAuthorizedHandler<DeleteBudgetCommand, Budget>
{
	public async Task HandleAsync(
		DeleteBudgetCommand command,
		Budget budget,
		CancellationToken ct = default
	) => await budgetWriteRepository.DeleteAsync(budgetId: budget.Id, ct: ct);
}