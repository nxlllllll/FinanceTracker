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

namespace FinanceTracker.Application.UseCases.Budget.Commands.ActivateBudget;

public sealed class ActivateBudgetHandler(
	IBudgetReadRepository budgetReadRepository,
	IBudgetWriteRepository budgetWriteRepository,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<ActivateBudgetCommand, Core.Domains.Budget.Budget, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ActivateBudgetCommand command,
		Core.Domains.Budget.Budget budget,
		CancellationToken ct = default)
	{
		Result<bool, DomainException> result = budget.Activate();
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
					from: budget.From,
					to: budget.To,
					excludeBudgetId: budget.Id,
					ct: ct
				);

				if (conflict is not null)
					return conflict;

				await budgetWriteRepository.ActivateAsync(
					budgetId: budget.Id,
					expectedVersion: budget.RowVersion,
					ct: ct
				);

				return null;
			}, ct: ct);
		}
		catch (UniqueConstraintException)
		{
			return Result<Guid, AppException>.Failure(error: new OverlappingBudgetException(
				message: "Cannot activate: another active budget covers this category during the same period. Deactivate it, or move this budget to different dates."
			));
		}

		if (conflictingBudgetId is not null)
		{
			return Result<Guid, AppException>.Failure(error: new OverlappingBudgetException(
				message: "Cannot activate: another active budget covers this category during the same period. Deactivate it, or move this budget to different dates.",
				conflictingBudgetId: conflictingBudgetId
			));
		}

		postCommitNotifications.Stage(notification: new BudgetActivatedNotification(
			BudgetId: budget.Id,
			UserId: budget.UserId,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: budget.Id);
	}
}
