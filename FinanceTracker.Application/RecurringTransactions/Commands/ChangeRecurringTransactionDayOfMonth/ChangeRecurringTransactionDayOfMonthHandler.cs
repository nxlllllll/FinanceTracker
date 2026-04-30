using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.RecurringTransaction;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionDayOfMonth;

public sealed class ChangeRecurringTransactionDayOfMonthHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository
) : IAuthorizedHandler<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransactionDto>
{
	public async Task HandleAsync(
		ChangeRecurringTransactionDayOfMonthCommand command,
		RecurringTransactionDto recurringTransaction,
		CancellationToken ct = default
	) => await recurringTransactionWriteRepository.ChangeDayOfMonthAsync(recurringTransactionId: command.RecurringTransactionId, dayOfMonth: command.DayOfMonth, ct: ct);
}