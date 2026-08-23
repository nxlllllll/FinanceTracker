using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionAmount;

public sealed class ChangeRecurringTransactionAmountHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeRecurringTransactionAmountCommand, Core.Domains.RecurringTransaction.RecurringTransaction, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeRecurringTransactionAmountCommand command,
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction,
		CancellationToken ct = default)
	{
		Result<bool, DomainException> result = recurringTransaction.ChangeAmount(amount: command.Amount);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (!result.Value)
			return Result<Guid, AppException>.Success(value: recurringTransaction.Id);

		await recurringTransactionWriteRepository.ChangeAmountAsync(
			recurringTransactionId: command.RecurringTransactionId,
			expectedVersion: recurringTransaction.RowVersion,
			amount: command.Amount,
			ct: ct
		);

		postCommitNotifications.Stage(notification: new RecurringTransactionAmountChangedNotification(
			RecurringTransactionId: recurringTransaction.Id,
			UserId: recurringTransaction.UserId,
			NewAmount: command.Amount,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: recurringTransaction.Id);
	}
}
