using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Budget;

namespace FinanceTracker.Application.Budgets.Commands.ChangeBudgetAmount;

public sealed class ChangeBudgetAmountHandler(
	IBudgetWriteRepository budgetWriteRepository
) : IAuthorizedHandler<ChangeBudgetAmountCommand, Budget>
{
	public async Task HandleAsync(
		ChangeBudgetAmountCommand command,
		Budget budget,
		CancellationToken ct = default
	)
	{
		budget.ChangeAmount(amount: command.Amount);
		
		await budgetWriteRepository.ChangeAmountAsync(budgetId: budget.Id, amount: command.Amount, ct: ct);
	}
}