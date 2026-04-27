using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Budget;
using MediatR;

namespace FinanceTracker.Application.Budgets.Commands.ChangeBudgetPeriod;

public sealed class ChangeBudgetPeriodHandler(
	IBudgetReadRepository budgetReadRepository,
	IBudgetWriteRepository budgetWriteRepository
) : IRequestHandler<ChangeBudgetPeriodCommand>
{
	public async Task Handle(
		ChangeBudgetPeriodCommand command,
		CancellationToken ct = default)
	{
		BudgetDto budget = await budgetReadRepository.GetByIdAsync(budgetId: command.BudgetId, userId: command.UserId, ct: ct)
			?? throw new NotFoundException(message: "Budget not found.", id: command.BudgetId);

		await budgetWriteRepository.ChangePeriodAsync(budgetId: budget.Id, dateFrom: command.From, dateTo: command.To, ct: ct);
	}
}