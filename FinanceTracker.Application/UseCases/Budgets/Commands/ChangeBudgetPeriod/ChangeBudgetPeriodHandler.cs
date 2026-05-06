using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Results;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Budgets.Commands.ChangeBudgetPeriod;

public sealed class ChangeBudgetPeriodHandler(
	IBudgetWriteRepository budgetWriteRepository,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	IUnitOfWork unitOfWork,
	ILogger<ChangeBudgetPeriodHandler> logger
) : IAuthorizedHandler<ChangeBudgetPeriodCommand, Budget, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeBudgetPeriodCommand command,
		Budget budget,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = budget.ChangePeriod(from: command.From, to: command.To);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
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
				fromDate: command.From,
				toDate: command.To,
				ct: ct
			);
		}, 
		onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to change period for budget {budget.Id} ({command.From} → {command.To})."),
		ct: ct);
		
		return Result<Guid, DomainException>.Success(value: budget.Id);
	}
}