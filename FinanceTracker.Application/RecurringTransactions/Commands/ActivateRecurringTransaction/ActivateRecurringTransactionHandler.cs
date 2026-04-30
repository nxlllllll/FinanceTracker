using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.RecurringTransaction;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ActivateRecurringTransaction;

public sealed class ActivateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository
) : IAuthorizedHandler<ActivateRecurringTransactionCommand, RecurringTransactionDto>
{
	public async Task HandleAsync(
		ActivateRecurringTransactionCommand command,
		RecurringTransactionDto recurringTransaction,
		CancellationToken ct = default)
	{
		if (recurringTransaction.IsActive)
			return;

		await recurringTransactionWriteRepository.ActivateAsync(recurringTransactionId: command.RecurringTransactionId, ct: ct);
	}
}