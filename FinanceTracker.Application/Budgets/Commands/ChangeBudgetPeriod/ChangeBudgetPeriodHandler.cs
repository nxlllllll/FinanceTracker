using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Budget;

namespace FinanceTracker.Application.Budgets.Commands.ChangeBudgetPeriod;

public sealed class ChangeBudgetPeriodHandler(
	IBudgetWriteRepository budgetWriteRepository
) : IAuthorizedHandler<ChangeBudgetPeriodCommand, BudgetDto>
{
	public async Task HandleAsync(
		ChangeBudgetPeriodCommand command,
		BudgetDto budget,
		CancellationToken ct = default
	) => await budgetWriteRepository.ChangePeriodAsync(budgetId: budget.Id, dateFrom: command.From, dateTo: command.To, ct: ct);
}