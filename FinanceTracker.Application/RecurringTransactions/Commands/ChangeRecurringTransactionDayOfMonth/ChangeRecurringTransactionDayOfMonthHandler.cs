using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.RecurringTransaction;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionDayOfMonth;

public sealed class ChangeRecurringTransactionDayOfMonthHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository
) : IAuthorizedHandler<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction>
{
	public async Task HandleAsync(
		ChangeRecurringTransactionDayOfMonthCommand command,
		RecurringTransaction recurringTransaction,
		CancellationToken ct = default
	)
	{
		recurringTransaction.ChangeDayOfMonth(dayOfMonth: command.DayOfMonth);
		
		await recurringTransactionWriteRepository.ChangeDayOfMonthAsync(
			recurringTransactionId: command.RecurringTransactionId,
			dayOfMonth: command.DayOfMonth,
			ct: ct
		);
	}
}