using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.RecurringTransaction;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;

public sealed class ChangeRecurringTransactionAmountHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository
) : IAuthorizedHandler<ChangeRecurringTransactionAmountCommand, RecurringTransaction>
{
	public async Task HandleAsync(
		ChangeRecurringTransactionAmountCommand command,
		RecurringTransaction recurringTransaction,
		CancellationToken ct = default
	)
	{
		recurringTransaction.ChangeAmount(amount: command.Amount);
		
		await recurringTransactionWriteRepository.ChangeAmountAsync(
			recurringTransactionId: command.RecurringTransactionId,
			amount: command.Amount,
			ct: ct
		);
	}
}