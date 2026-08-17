using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Budget;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Data;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetPeriod;

public sealed class ChangeBudgetPeriodHandler(
	IBudgetReadRepository budgetReadRepository,
	IBudgetWriteRepository budgetWriteRepository,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider,
	ILogger<ChangeBudgetPeriodHandler> logger
) : IAuthorizedHandler<ChangeBudgetPeriodCommand, Core.Domains.Budget.Budget, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeBudgetPeriodCommand command,
		Core.Domains.Budget.Budget budget,
		CancellationToken ct = default)
	{
		Result<bool, DomainException> result = budget.ChangePeriod(from: command.From, to: command.To);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (!result.Value)
			return Result<Guid, AppException>.Success(value: budget.Id);

		Guid? conflictingBudgetId;

		try
		{
			conflictingBudgetId = await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				Guid? conflict = await budgetReadRepository.FindOverlappingAsync(
					userId: command.UserId,
					categoryId: budget.CategoryId,
					from: command.From,
					to: command.To,
					excludeBudgetId: budget.Id,
					ct: ct
				);

				if (conflict is not null)
					return conflict;

				await budgetWriteRepository.ChangePeriodAsync(
					budgetId: budget.Id,
					from: command.From,
					to: command.To,
					expectedVersion: budget.RowVersion,
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

				return null;
			},
			onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to change period for budget {budget.Id} ({command.From} > {command.To})."),
			ct: ct);
		}
		catch (UniqueConstraintException)
		{
			return Result<Guid, AppException>.Failure(error: new OverlappingBudgetException(
				message: "Another budget for this category already covers part of the requested period."
			));
		}

		if (conflictingBudgetId is not null)
		{
			return Result<Guid, AppException>.Failure(error: new OverlappingBudgetException(
				message: "Another budget for this category already covers part of the requested period.",
				conflictingBudgetId: conflictingBudgetId
			));
		}

		postCommitNotifications.Stage(notification: new BudgetPeriodChangedNotification(
			BudgetId: budget.Id,
			UserId: budget.UserId,
			NewFrom: command.From,
			NewTo: command.To,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: budget.Id);
	}
}
