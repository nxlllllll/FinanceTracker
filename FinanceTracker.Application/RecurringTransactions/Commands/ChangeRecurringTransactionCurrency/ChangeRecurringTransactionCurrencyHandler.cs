using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;

public sealed class ChangeRecurringTransactionCurrencyHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<ChangeRecurringTransactionCurrencyCommand>
{
	public async Task Handle(
		ChangeRecurringTransactionCurrencyCommand command,
		CancellationToken ct = default)
	{
		RecurringTransactionDto recurringTransaction = await recurringTransactionReadRepository.GetByIdAsync(recurringTransactionId: command.RecurringTransactionId, ct: ct)
			?? throw new NotFoundException(message: "Recurring transaction not found.", id: command.RecurringTransactionId);

		if (recurringTransaction.UserId != command.UserId)
			throw new NotFoundException(message: "Recurring transaction not found.", id: command.RecurringTransactionId);

		await recurringTransactionWriteRepository.ChangeCurrencyAsync(recurringTransactionId: command.RecurringTransactionId, currency: command.Currency, ct: ct);
	}
}