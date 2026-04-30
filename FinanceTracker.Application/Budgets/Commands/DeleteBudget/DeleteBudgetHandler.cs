using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Budget;

namespace FinanceTracker.Application.Budgets.Commands.DeleteBudget;

public sealed class DeleteBudgetHandler(
	IBudgetWriteRepository budgetWriteRepository
) : IAuthorizedHandler<DeleteBudgetCommand, BudgetDto>
{
	public async Task HandleAsync(
		DeleteBudgetCommand command,
		BudgetDto budget,
		CancellationToken ct = default
	) => await budgetWriteRepository.DeleteAsync(budgetId: budget.Id, ct: ct);
}