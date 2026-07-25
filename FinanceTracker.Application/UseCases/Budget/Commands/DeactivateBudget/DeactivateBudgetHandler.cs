using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.UseCases.Budget.Commands.DeactivateBudget;

public sealed class DeactivateBudgetHandler(
	IBudgetWriteRepository budgetWriteRepository,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<DeactivateBudgetCommand, Core.Domains.Budget.Budget, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		DeactivateBudgetCommand command,
		Core.Domains.Budget.Budget user,
		CancellationToken ct = default)
	{
		Result<bool, DomainException> result = user.Deactivate();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (!result.Value)
			return Result<Guid, AppException>.Success(value: user.Id);

		await budgetWriteRepository.DeactivateAsync(budgetId: user.Id, expectedVersion: user.RowVersion, ct: ct);

		postCommitNotifications.Stage(notification: new BudgetDeactivatedNotification(
			BudgetId: user.Id,
			UserId: user.UserId,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
