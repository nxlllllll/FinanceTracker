using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Repositories.Budget;

namespace FinanceTracker.Application.Budgets.Commands.DeleteBudget;

public sealed class DeleteBudgetHandler(
	IBudgetWriteRepository budgetWriteRepository
) : IAuthorizedHandler<DeleteBudgetCommand, Budget, Guid>
{
	public async Task<Guid> HandleAsync(
		DeleteBudgetCommand command,
		Budget budget,
		CancellationToken ct = default)
	{
		await budgetWriteRepository.DeleteAsync(budgetId: budget.Id, ct: ct);
		
		return budget.Id;
	}
}