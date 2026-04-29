using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Budget;
using MediatR;

namespace FinanceTracker.Application.Budgets.Commands.ChangeBudgetAmount;

public sealed class ChangeBudgetAmountHandler(
	IBudgetReadRepository budgetReadRepository,
	IBudgetWriteRepository budgetWriteRepository
) : IRequestHandler<ChangeBudgetAmountCommand>
{
	public async Task Handle(
		ChangeBudgetAmountCommand command,
		CancellationToken ct = default)
	{
		BudgetDto budget = await budgetReadRepository.GetByIdAsync(budgetId: command.BudgetId, userId: command.UserId, ct: ct) 
			?? throw new NotFoundException(message: "Budget not found.", id: command.BudgetId);

		if (budget.UserId != command.UserId)
			throw new NotFoundException(message: "Budget not found.", id: command.BudgetId);
		
		await budgetWriteRepository.ChangeAmountAsync(budgetId: budget.Id, amount: command.Amount, ct: ct);
	}
}