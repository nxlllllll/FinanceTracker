using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;

public sealed class ChangeRecurringTransactionAmountHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<ChangeRecurringTransactionAmountCommand>
{
	public async Task Handle(
		ChangeRecurringTransactionAmountCommand command,
		CancellationToken ct = default)
	{
		RecurringTransactionDto recurringTransaction = await recurringTransactionReadRepository.GetByIdAsync(recurringTransactionId: command.RecurringTransactionId, ct: ct)
			?? throw new NotFoundException(message: "Recurring transaction not found.", id: command.RecurringTransactionId);

		if (recurringTransaction.UserId != command.UserId)
			throw new NotFoundException(message: "Recurring transaction not found.", id: command.RecurringTransactionId);

		await recurringTransactionWriteRepository.ChangeAmountAsync(recurringTransactionId: command.RecurringTransactionId, amount: command.Amount, ct: ct);
	}
}