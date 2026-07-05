using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionDayOfMonth;

public sealed class ChangeRecurringTransactionDayOfMonthHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<ChangeRecurringTransactionDayOfMonthHandler> logger
) : IAuthorizedHandler<ChangeRecurringTransactionDayOfMonthCommand, Core.Domains.RecurringTransaction.RecurringTransaction, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeRecurringTransactionDayOfMonthCommand command,
		Core.Domains.RecurringTransaction.RecurringTransaction user,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = user.ChangeDayOfMonth(dayOfMonth: command.DayOfMonth);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await recurringTransactionWriteRepository.ChangeDayOfMonthAsync(
			recurringTransactionId: command.RecurringTransactionId,
			expectedVersion: user.RowVersion,
			dayOfMonth: command.DayOfMonth,
			ct: ct
		);

		try
		{
			await publisher.Publish(notification: new RecurringTransactionDayOfMonthChangedNotification(
				RecurringTransactionId: user.Id,
				UserId: user.UserId,
				NewDayOfMonth: command.DayOfMonth,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish RecurringTransactionDayOfMonthChangedNotification for recurring transaction {user.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
