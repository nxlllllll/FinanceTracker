using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.BudgetProgress;

namespace FinanceTracker.Application.Budgets.Commands.ChangeBudgetPeriod;

public sealed class ChangeBudgetPeriodHandler(
	IBudgetWriteRepository budgetWriteRepository,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	IUnitOfWork unitOfWork
) : IAuthorizedHandler<ChangeBudgetPeriodCommand, Budget>
{
	public async Task HandleAsync(
		ChangeBudgetPeriodCommand command,
		Budget budget,
		CancellationToken ct = default)
	{
		budget.ChangePeriod(from: command.From, to: command.To);

		await unitOfWork.BeginTransactionAsync(ct: ct);

		try
		{
			await budgetWriteRepository.ChangePeriodAsync(
				budgetId: budget.Id,
				dateFrom: command.From,
				dateTo: command.To,
				ct: ct
			);

			await budgetProgressWriteRepository.RecalculateForBudgetAsync(
				budgetId: budget.Id,
				userId: command.UserId,
				categoryId: budget.CategoryId,
				from: command.From,
				to: command.To,
				ct: ct
			);

			await unitOfWork.CommitAsync(ct: ct);
		}
		catch
		{
			await unitOfWork.RollbackAsync(ct: ct);
			throw;
		}
	}
}