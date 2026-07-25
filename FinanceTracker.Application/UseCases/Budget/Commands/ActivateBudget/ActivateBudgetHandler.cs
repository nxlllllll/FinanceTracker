using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
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
		Core.Domains.Budget.Budget user,
		CancellationToken ct = default)
	{
		Result<bool, DomainException> result = user.Activate();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (!result.Value)
			return Result<Guid, AppException>.Success(value: user.Id);

		bool hasOverlap;

		try
		{
			hasOverlap = await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				bool overlap = await budgetReadRepository.HasOverlappingAsync(
					userId: command.UserId,
					categoryId: user.CategoryId,
					from: user.From,
					to: user.To,
					excludeBudgetId: user.Id,
					ct: ct
				);

				if (overlap)
					return true;

				await budgetWriteRepository.ActivateAsync(budgetId: user.Id, expectedVersion: user.RowVersion, ct: ct);

				return false;
			}, ct: ct);
		}
		catch (UniqueConstraintException)
		{
			hasOverlap = true;
		}

		if (hasOverlap)
		{
			return Result<Guid, AppException>.Failure(error: new OverlappingBudgetException(
				message: "Cannot activate: a budget for this category already exists in an overlapping period."
			));
		}

		postCommitNotifications.Stage(notification: new BudgetActivatedNotification(
			BudgetId: user.Id,
			UserId: user.UserId,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
