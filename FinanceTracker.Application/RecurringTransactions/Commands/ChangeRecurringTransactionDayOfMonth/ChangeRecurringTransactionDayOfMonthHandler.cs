using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionDayOfMonth;

public sealed class ChangeRecurringTransactionDayOfMonthHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<ChangeRecurringTransactionDayOfMonthCommand>
{
	public async Task Handle(
		ChangeRecurringTransactionDayOfMonthCommand command,
		CancellationToken ct = default)
	{
		RecurringTransactionDto recurringTransaction = await recurringTransactionReadRepository.GetByIdAsync(recurringTransactionId: command.RecurringTransactionId, ct: ct)
			?? throw new NotFoundException(message: "Recurring transaction not found.", id: command.RecurringTransactionId);

		if (recurringTransaction.UserId != command.UserId)
			throw new NotFoundException(message: "Recurring transaction not found.", id: command.RecurringTransactionId);

		await recurringTransactionWriteRepository.ChangeDayOfMonthAsync(recurringTransactionId: command.RecurringTransactionId, dayOfMonth: command.DayOfMonth, ct: ct);
	}
}