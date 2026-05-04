using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Repositories.Budget;

namespace FinanceTracker.Application.Budgets.Commands.ChangeBudgetAmount;

public sealed class ChangeBudgetAmountHandler(
	IBudgetWriteRepository budgetWriteRepository
) : IAuthorizedHandler<ChangeBudgetAmountCommand, Budget, Guid>
{
	public async Task<Guid> HandleAsync(
		ChangeBudgetAmountCommand command,
		Budget budget,
		CancellationToken ct = default)
	{
		budget.ChangeAmount(amount: command.Amount);
		await budgetWriteRepository.ChangeAmountAsync(budgetId: budget.Id, amount: command.Amount, ct: ct);
		
		return budget.Id;
	}
}