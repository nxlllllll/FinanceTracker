using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;

public sealed class ChangeRecurringTransactionCurrencyHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository
) : IAuthorizedHandler<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction, Guid>
{
	public async Task<Guid> HandleAsync(
		ChangeRecurringTransactionCurrencyCommand command,
		RecurringTransaction recurringTransaction,
		CancellationToken ct = default)
	{
		recurringTransaction.ChangeCurrency(currency: command.Currency);
		await recurringTransactionWriteRepository.ChangeCurrencyAsync(
			recurringTransactionId: command.RecurringTransactionId,
			currency: command.Currency,
			ct: ct
		);
		
		return recurringTransaction.Id;
	}
}