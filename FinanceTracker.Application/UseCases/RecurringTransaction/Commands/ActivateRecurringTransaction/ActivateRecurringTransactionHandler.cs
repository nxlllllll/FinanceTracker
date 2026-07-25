using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ActivateRecurringTransaction;

public sealed class ActivateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<ActivateRecurringTransactionCommand, Core.Domains.RecurringTransaction.RecurringTransaction, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ActivateRecurringTransactionCommand command,
		Core.Domains.RecurringTransaction.RecurringTransaction user,
		CancellationToken ct = default)
	{
		Result<bool, DomainException> result = user.Activate();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (!result.Value)
			return Result<Guid, AppException>.Success(value: user.Id);

		await recurringTransactionWriteRepository.ActivateAsync(
			recurringTransactionId: command.RecurringTransactionId,
			expectedVersion: user.RowVersion,
			ct: ct
		);

		postCommitNotifications.Stage(notification: new RecurringTransactionActivatedNotification(
			RecurringTransactionId: user.Id,
			UserId: user.UserId,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
