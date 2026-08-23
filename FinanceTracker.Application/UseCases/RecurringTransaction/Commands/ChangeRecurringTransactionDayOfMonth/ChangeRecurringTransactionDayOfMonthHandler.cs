using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionDayOfMonth;

public sealed class ChangeRecurringTransactionDayOfMonthHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IUserQueryRepository userQueryRepository,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeRecurringTransactionDayOfMonthCommand, Core.Domains.RecurringTransaction.RecurringTransaction, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeRecurringTransactionDayOfMonthCommand command,
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction,
		CancellationToken ct = default)
	{
		TimeZoneId? timeZone = await userQueryRepository.GetTimeZoneAsync(userId: recurringTransaction.UserId, ct: ct);
		if (timeZone is null)
			return Result<Guid, AppException>.Failure(error: new NotFoundException(message: "User not found.", id: recurringTransaction.UserId));

		Result<bool, DomainException> result = recurringTransaction.ChangeDayOfMonth(
			dayOfMonth: command.DayOfMonth,
			timeZone: timeZone.Value,
			now: dateProvider.UtcNow
		);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (!result.Value)
			return Result<Guid, AppException>.Success(value: recurringTransaction.Id);

		await recurringTransactionWriteRepository.ChangeDayOfMonthAsync(
			recurringTransactionId: command.RecurringTransactionId,
			expectedVersion: recurringTransaction.RowVersion,
			dayOfMonth: command.DayOfMonth,
			nextDueAtUtc: recurringTransaction.NextDueAtUtc,
			ct: ct
		);

		postCommitNotifications.Stage(notification: new RecurringTransactionDayOfMonthChangedNotification(
			RecurringTransactionId: recurringTransaction.Id,
			UserId: recurringTransaction.UserId,
			NewDayOfMonth: command.DayOfMonth,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: recurringTransaction.Id);
	}
}
