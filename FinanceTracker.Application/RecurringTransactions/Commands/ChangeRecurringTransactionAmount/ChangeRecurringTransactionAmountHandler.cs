using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.RecurringTransaction;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;

public sealed class ChangeRecurringTransactionAmountHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository
) : IAuthorizedHandler<ChangeRecurringTransactionAmountCommand, RecurringTransactionDto>
{
	public async Task HandleAsync(
		ChangeRecurringTransactionAmountCommand command,
		RecurringTransactionDto recurringTransaction,
		CancellationToken ct = default
	) => await recurringTransactionWriteRepository.ChangeAmountAsync(recurringTransactionId: command.RecurringTransactionId, amount: command.Amount, ct: ct);
}