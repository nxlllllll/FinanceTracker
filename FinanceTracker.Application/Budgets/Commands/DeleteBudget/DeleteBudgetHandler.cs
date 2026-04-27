using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Budget;
using MediatR;

namespace FinanceTracker.Application.Budgets.Commands.DeleteBudget;

public sealed class DeleteBudgetHandler(
	IBudgetReadRepository budgetReadRepository,
	IBudgetWriteRepository budgetWriteRepository
) : IRequestHandler<DeleteBudgetCommand>
{
	public async Task Handle(
		DeleteBudgetCommand command,
		CancellationToken ct = default)
	{
		BudgetDto budget = await budgetReadRepository.GetByIdAsync(budgetId: command.BudgetId, userId: command.UserId, ct: ct)
			?? throw new NotFoundException(message: "Budget not found.", id: command.BudgetId);

		await budgetWriteRepository.DeleteAsync(budgetId: budget.Id, ct: ct);
	}
}