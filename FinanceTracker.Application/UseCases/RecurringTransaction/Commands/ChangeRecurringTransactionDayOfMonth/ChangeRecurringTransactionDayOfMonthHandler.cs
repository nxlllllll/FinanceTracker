using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionDayOfMonth;

public sealed class ChangeRecurringTransactionDayOfMonthHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeRecurringTransactionDayOfMonthCommand, Core.Domains.RecurringTransaction.RecurringTransaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeRecurringTransactionDayOfMonthCommand command,
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = recurringTransaction.ChangeDayOfMonth(dayOfMonth: command.DayOfMonth);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await recurringTransactionWriteRepository.ChangeDayOfMonthAsync(
			recurringTransactionId: command.RecurringTransactionId,
			expectedVersion: recurringTransaction.RowVersion,
			dayOfMonth: command.DayOfMonth,
			ct: ct
		);
		
		await publisher.Publish(notification: new RecurringTransactionDayOfMonthChangedNotification(
			RecurringTransactionId: recurringTransaction.Id,
			UserId: recurringTransaction.UserId,
			NewDayOfMonth: command.DayOfMonth,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);
		
		return Result<Guid, DomainException>.Success(value: recurringTransaction.Id);
	}
}
