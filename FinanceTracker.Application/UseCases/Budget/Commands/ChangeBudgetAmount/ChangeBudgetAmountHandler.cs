using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetAmount;

public sealed class ChangeBudgetAmountHandler(
	IBudgetWriteRepository budgetWriteRepository,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeBudgetAmountCommand, Core.Domains.Budget.Budget, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeBudgetAmountCommand command,
		Core.Domains.Budget.Budget user,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = user.ChangeAmount(amount: command.Amount);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await budgetWriteRepository.ChangeAmountAsync(budgetId: user.Id, expectedVersion: user.RowVersion, amount: command.Amount, ct: ct);

		postCommitNotifications.Stage(notification: new BudgetAmountChangedNotification(
			BudgetId: user.Id,
			UserId: user.UserId,
			NewAmount: command.Amount,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
