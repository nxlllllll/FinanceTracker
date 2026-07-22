using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.DeactivateRecurringTransaction;

public sealed class DeactivateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<DeactivateRecurringTransactionCommand, Core.Domains.RecurringTransaction.RecurringTransaction, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		DeactivateRecurringTransactionCommand command,
		Core.Domains.RecurringTransaction.RecurringTransaction user,
		CancellationToken ct = default)
	{
		Result<bool, DomainException> result = user.Deactivate();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (!result.Value)
			return Result<Guid, AppException>.Success(value: user.Id);

		await recurringTransactionWriteRepository.DeactivateAsync(
			recurringTransactionId: command.RecurringTransactionId,
			expectedVersion: user.RowVersion,
			ct: ct
		);

		postCommitNotifications.Stage(notification: new RecurringTransactionDeactivatedNotification(
			RecurringTransactionId: user.Id,
			UserId: user.UserId,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
