using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Repositories.Budget;

namespace FinanceTracker.Application.Budgets.Commands.ChangeBudgetPeriod;

public sealed class ChangeBudgetPeriodHandler(
	IBudgetWriteRepository budgetWriteRepository
) : IAuthorizedHandler<ChangeBudgetPeriodCommand, Budget>
{
	public async Task HandleAsync(
		ChangeBudgetPeriodCommand command,
		Budget budget,
		CancellationToken ct = default
	)
	{
		budget.ChangePeriod(from: command.From, to: command.To);
		
		await budgetWriteRepository.ChangePeriodAsync(
			budgetId: budget.Id,
			dateFrom: command.From,
			dateTo: command.To,
			ct: ct
		);
	}
}